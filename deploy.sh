#!/bin/bash

export AWS_PAGER=""

awslocal() {
    export AWS_ACCESS_KEY_ID="test"
    export AWS_SECRET_ACCESS_KEY="test"
    export AWS_DEFAULT_REGION=${DEFAULT_REGION:-${AWS_DEFAULT_REGION:-"eu-central-1"}}
    local localstack_host=${LOCALSTACK_HOST:-"localhost"}
    local localstack_url="http://$localstack_host:4566"
    aws "$@" --endpoint-url $localstack_url
}

echo -n "Enter 'aws' for AWS or 'localstack' for LocalStack (default is 'localstack'):"
read target
if [ -z "$target" ]; then target="localstack"; fi

if [ "$target" == "localstack" ]; then
    awsFunc() { awslocal "$@"; }
else
    awsFunc() { aws "$@" --profile personnal; }
fi

defaultRegion="eu-central-1"
profileServicePath="./src/ProfileService/"
profileServiceArtifactPath="./artifacts/profile-service.zip"
functionName="profile-service"
roleName="${functionName}-role"
bucketName="${functionName}-bucket"
tableName="${functionName}-table"
queueName="${functionName}-queue"

echo -n "Enter 'deploy' to create resources or 'cleanup' to delete resources (default is 'deploy'):"
read operation
if [ -z "$operation" ]; then operation="deploy"; fi

if [ "$operation" == "cleanup" ]; then
    echo -n "Are you sure you want to delete all resources? (yes/no)"
    read confirmCleanup
    if [ "$confirmCleanup" == "yes" ]; then
        # Delete the Lambda function
        echo "Deleting Lambda function..."
        awsFunc lambda delete-function --function-name $functionName

        # Detach Basic Execution role policy and delete the IAM role
        echo "Detaching basic execution policy from role..."
        awsFunc iam detach-role-policy --role-name $roleName --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
        echo "Deleting IAM role..."
        awsFunc iam delete-role --role-name $roleName

        # Delete the SQS queue
        echo "Deleting SQS queue..."
        queueUrl=$(awsFunc sqs get-queue-url --queue-name $queueName --query QueueUrl --output text)
        awsFunc sqs delete-queue --queue-url $queueUrl

        # Delete the DynamoDB table
        echo "Deleting DynamoDB table..."
        awsFunc dynamodb delete-table --table-name $tableName

        # Delete the S3 bucket (ensure it is empty first)
        echo "Emptying S3 bucket..."
        awsFunc s3 rm s3://$bucketName --recursive
        echo "Deleting S3 bucket..."
        awsFunc s3api delete-bucket --bucket $bucketName

        echo "Cleanup complete!"
        exit 0
    else
        echo "Cleanup cancelled."
        exit 1
    fi
fi

IsResourceExists() {
    resourceType="$1"
    resourceName="$2"

    if [ "$resourceType" == "S3 bucket" ]; then
        result=$(awsFunc s3api head-bucket --bucket "$resourceName" 2>&1)
        if [[ $result == *"Not Found"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    elif [ "$resourceType" == "DynamoDB table" ]; then
        result=$(awsFunc dynamodb describe-table --table-name "$resourceName" 2>&1)
        if [[ $result == *"ResourceNotFoundException"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    elif [ "$resourceType" == "SQS queue" ]; then
        result=$(awsFunc sqs get-queue-url --queue-name "$resourceName" 2>&1)
        if [[ $result == *"NonExistentQueue"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    elif [ "$resourceType" == "IAM role" ]; then
        result=$(awsFunc iam get-role --role-name "$resourceName" 2>&1)
        if [[ $result == *"NoSuchEntity"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    elif [ "$resourceType" == "Lambda function" ]; then
        result=$(awsFunc lambda get-function --function-name "$resourceName" 2>&1)
        if [[ $result == *"ResourceNotFoundException"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    else
        echo "Unknown resource type: $resourceType"
        echo "0"
    fi
}

# Check and create S3 bucket
bucketExists=$(IsResourceExists "S3 bucket" "$bucketName")

if [ "$bucketExists" == "0" ]; then
    echo "Creating S3 bucket..."
    awsFunc s3api create-bucket --bucket $bucketName --region $defaultRegion --create-bucket-configuration LocationConstraint=$defaultRegion
else
    echo "S3 bucket already exists, skipping creation."
fi

# Check and create DynamoDB table
tableExists=$(IsResourceExists "DynamoDB table" $tableName)

if [ "$tableExists" == "0" ]; then
    echo "Creating DynamoDB table..."
    awsFunc dynamodb create-table --table-name $tableName --attribute-definitions 'AttributeName=Id,AttributeType=S' --key-schema 'AttributeName=Id,KeyType=HASH' --provisioned-throughput 'ReadCapacityUnits=5,WriteCapacityUnits=5'
else
    echo "DynamoDB table already exists, skipping creation."
fi

# Check and create SQS queue
queueExists=$(IsResourceExists "SQS queue" $queueName)

if [ "$queueExists" == "0" ]; then
    echo "Creating SQS queue..."
    awsFunc sqs create-queue --queue-name $queueName
else
    echo "SQS queue already exists, skipping creation."
fi

# Check and create IAM role
roleExists=$(IsResourceExists "IAM role" $roleName)

if [ "$roleExists" == "0" ]; then
    echo "Creating IAM role..."
    assumeRolePolicy=$(
        cat <<-EOM
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Service": "lambda.amazonaws.com" },
      "Action": "sts:AssumeRole"
    },
    {
      "Effect": "Allow",
      "Action": ["s3:*", "dynamodb:*", "sqs:*"],
      "Resource": [
        "arn:aws:s3:::$bucketName/*",
        "arn:aws:dynamodb:*:*:table/$tableName",
        "arn:aws:sqs:*:*:$queueName"
      ]
    }
  ]
}
EOM
    )
    awsFunc iam create-role --role-name $roleName --assume-role-policy-document "$assumeRolePolicy"
    awsFunc iam attach-role-policy --role-name $roleName --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
else
    echo "IAM role already exists, skipping creation."
fi

# Check and create/update Lambda function
lambdaFunctionExists=$(IsResourceExists "Lambda function" $functionName)

if [ "$lambdaFunctionExists" == "0" ]; then
    echo "Packaging Lambda function..."
    dotnet lambda package --project-location $profileServicePath --output-package $profileServiceArtifactPath

    echo "Creating Lambda function..."
    roleArn=$(awsFunc iam get-role --role-name $roleName --query Role.Arn --output text)
    awsFunc lambda create-function --function-name $functionName --zip-file fileb://$profileServiceArtifactPath --handler bootstrap --runtime provided.al2 --role $roleArn --memory-size 256 --timeout 30
else
    echo -n "Lambda function already exists. Do you want to update it? (yes/no)"
    read update
    if [ "$update" == "yes" ]; then
        echo "Packaging Lambda function..."
        dotnet lambda package --project-location $profileServicePath --output-package $profileServiceArtifactPath

        echo "Updating Lambda function..."
        awsFunc lambda update-function-code --function-name $functionName --zip-file fileb://$profileServiceArtifactPath
    else
        echo "Skipping update of Lambda function."
    fi
fi

echo "Setup complete!"
