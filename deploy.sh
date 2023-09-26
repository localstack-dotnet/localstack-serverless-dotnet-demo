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
    echo -n "Enter AWS profile name:"
    read profileName
    if [ -z "$profileName" ]; then
        echo "Please provide a valid AWS profile name."
        exit 1
    fi
    awsFunc() { aws "$@" --profile $profileName; }
fi

# Prompt the user for AWS region
read -p "Enter AWS region (default is 'eu-central-1'):" enteredRegion
defaultRegion=${enteredRegion:-"eu-central-1"}

lambdaDotNetEnv="Production"
if [ "$target" == "localstack" ]; then
    lambdaDotNetEnv="Development"
fi

accountID=$(awsFunc sts get-caller-identity --query Account --output text)
if [ -z "$accountID" ]; then
    echo "Failed to fetch AWS account ID. Exiting script."
    exit 1
fi

# Known project location and artifact path
profileServicePath="./src/LocalStack.Services.ProfileApi/"
profileServicePublishPath="./src/LocalStack.Services.ProfileApi/bin/Release/net7.0/publish"
profileServiceArtifactPath="./artifacts/profile-service.zip"

# Known project location and artifact path for the message handler
messageHandlerServicePath="./src/LocalStack.Services.MessageHandler/"
messageHandlerServiceArtifactPath="./artifacts/message-handler-service.zip"

# Derived resource names
functionName="profile-service-demo"
roleName="${functionName}-role"
policyName="${functionName}-policy"
bucketName="${functionName}-bucket"
tableName="${functionName}-table"
queueName="${functionName}-queue"

# Derived resource names for the message handler
messageHandlerFunctionName="message-handler-demo"
messagesTableName="${messageHandlerFunctionName}-table"

# Prompt user for operation
echo -n "Enter 'deploy' to create resources or 'cleanup' to delete resources (default is 'deploy'):"
read operation
if [ -z "$operation" ]; then operation="deploy"; fi

if [ "$operation" == "cleanup" ]; then
    echo -n "Are you sure you want to delete all resources? (yes/no)"
    read confirmCleanup
    if [ "$confirmCleanup" == "yes" ]; then
        echo "Cleaning up resources..."

        # Delete the Lambda function
        echo "Deleting Lambda function..."
        awsFunc lambda delete-function --function-name $functionName

        # Delete the message handler Lambda function
        echo "Deleting message handler Lambda function..."
        awsFunc lambda delete-function --function-name $messageHandlerFunctionName

        # Get a list of all policies attached to the role
        policies=$(awsFunc iam list-attached-role-policies --role-name $roleName --query 'AttachedPolicies[].PolicyArn' --output text)

        echo $policies

        # Detach each policy
        IFS=$' \t\n' # <-- Set IFS to handle spaces, tabs, and newlines
        for policyArn in $policies; do
            echo "Detaching policy $policyArn from role $roleName..."
            awsFunc iam detach-role-policy --role-name $roleName --policy-arn $policyArn

            # Only delete customer-managed policies
            if [[ "$policyArn" == *":iam::$accountID:policy/"* ]]; then
                # Delete the policy
                echo "Deleting customer-managed policy: $policyArn"
                awsFunc iam delete-policy --policy-arn $policyArn
            fi
        done
        unset IFS # <-- Reset the IFS back to its original value

        echo "Deleting IAM role..."
        awsFunc iam delete-role --role-name $roleName

        # Delete the SQS queue
        echo "Deleting SQS queue..."
        queueUrl=$(awsFunc sqs get-queue-url --queue-name $queueName --query QueueUrl --output text)
        queueAttributes=$(awsFunc sqs get-queue-attributes --queue-url $queueUrl --attribute-names QueueArn)
        queueArn=$(echo $queueAttributes | jq -r .Attributes.QueueArn)
        awsFunc sqs delete-queue --queue-url $queueUrl

        echo "Deleting event source mappings..."
        existingMappings=$(awsFunc lambda list-event-source-mappings --function-name message-handler-demo)
        eventSourceMappings=$(echo $existingMappings | jq -c '.EventSourceMappings[] | select(.EventSourceArn == "'$queueArn'")')

        IFS=$'\n'
        for mapping in $eventSourceMappings; do
            mappingUUID=$(echo $mapping | jq -r .UUID)

            if [ ! -z "$mappingUUID" ]; then
                echo "Deleting event source mapping with UUID: $mappingUUID..."
                awsFunc lambda delete-event-source-mapping --uuid $mappingUUID
            fi
        done
        unset IFS

        # Delete the DynamoDB table
        echo "Deleting DynamoDB table..."
        awsFunc dynamodb delete-table --table-name $tableName

        # Delete the Messages DynamoDB table
        echo "Deleting Messages DynamoDB table..."
        awsFunc dynamodb delete-table --table-name $messagesTableName

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

    if [[ "$resourceType" == *"S3 bucket"* ]]; then
        result=$(awsFunc s3api head-bucket --bucket "$resourceName" 2>&1)
        if [[ $result == *"Not Found"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    elif [[ "$resourceType" == *"DynamoDB table"* ]]; then
        result=$(awsFunc dynamodb describe-table --table-name "$resourceName" 2>&1)
        if [[ $result == *"ResourceNotFoundException"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    elif [[ "$resourceType" == *"SQS queue"* ]]; then
        result=$(awsFunc sqs get-queue-url --queue-name "$resourceName" 2>&1)
        if [[ $result == *"NonExistentQueue"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    elif [[ "$resourceType" == *"IAM role"* ]]; then
        result=$(awsFunc iam get-role --role-name "$resourceName" 2>&1)
        if [[ $result == *"NoSuchEntity"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    elif [[ "$resourceType" == *"Lambda function"* ]]; then
        result=$(awsFunc lambda get-function --function-name "$resourceName" 2>&1)
        if [[ $result == *"ResourceNotFoundException"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    elif [[ "$resourceType" == *"IAM policy"* ]]; then
        result=$(awsFunc iam get-policy --policy-arn "$resourceName" 2>&1)
        if [[ $result == *"NoSuchEntity"* ]]; then
            echo "0"
        else
            echo "1"
        fi
    else
        echo "Unknown resource type: $resourceType"
        echo "0"
    fi
}

# Check S3 bucket
bucketExists=$(IsResourceExists "S3 bucket" "$bucketName")

if [ "$bucketExists" == "0" ]; then
    echo "Creating S3 bucket..."
    awsFunc s3api create-bucket --bucket $bucketName --region $defaultRegion --create-bucket-configuration LocationConstraint=$defaultRegion
else
    echo "S3 bucket already exists, skipping creation."
fi

# Check DynamoDB table
tableExists=$(IsResourceExists "DynamoDB table" $tableName)

if [ "$tableExists" == "0" ]; then
    echo "Creating DynamoDB table..."
    awsFunc dynamodb create-table --table-name $tableName --attribute-definitions AttributeName=Id,AttributeType=S --key-schema AttributeName=Id,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5
else
    echo "DynamoDB table already exists, skipping creation."
fi

# Check DynamoDB table for messages
messagesTableExists=$(IsResourceExists "DynamoDB table" $messagesTableName)

if [ "$messagesTableExists" == "0" ]; then
    echo "Creating DynamoDB table (messages)..."
    awsFunc dynamodb create-table --table-name $messagesTableName --attribute-definitions AttributeName=Id,AttributeType=S --key-schema AttributeName=Id,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5
else
    echo "DynamoDB table (messages) already exists, skipping creation."
fi

# Check SQS queue
queueExists=$(IsResourceExists "SQS queue" $queueName)

if [ "$queueExists" == "0" ]; then
    echo "Creating SQS queue..."
    awsFunc sqs create-queue --queue-name $queueName
else
    echo "SQS queue already exists, skipping creation."
fi

# Check IAM role
roleExists=$(IsResourceExists "IAM role" $roleName)

if [ "$roleExists" == "0" ]; then
    echo "Creating IAM role..."

    # Define assume role policy
    assumeRolePolicy=$(
        cat <<EOM
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Service": "lambda.amazonaws.com" },
      "Action": "sts:AssumeRole"
    }
  ]
}
EOM
    )
    tempFile=$(mktemp)
    echo $assumeRolePolicy >$tempFile
    awsFunc iam create-role --role-name $roleName --assume-role-policy-document file://$tempFile
    rm -f $tempFile

    # Define permission policy
    permissionPolicy=$(
        cat <<EOM
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:*", "dynamodb:*", "sqs:*"],
      "Resource": [
        "arn:aws:s3:::$bucketName/*",
        "arn:aws:dynamodb:*:*:table/$tableName",
        "arn:aws:sqs:*:*:$queueName",
        "arn:aws:dynamodb:*:*:table/$messagesTableName"
      ]
    }
  ]
}
EOM
    )
    tempFilePermissionPolicy=$(mktemp)
    echo $permissionPolicy >$tempFilePermissionPolicy

    # Check if the policy exists
    policyArnStr="arn:aws:iam::${accountID}:policy/$policyName"
    policyExists=$(IsResourceExists "IAM policy" $policyArnStr)

    if [ "$policyExists" == "0" ]; then
        echo "Creating IAM policy..."
        policyArn=$(awsFunc iam create-policy --policy-name $policyName --policy-document file://$tempFilePermissionPolicy --query 'Policy.Arn' --output text)
    else
        echo "IAM policy already exists. Retrieving its ARN..."
        policyArn=$(awsFunc iam get-policy --policy-arn $policyArnStr --query Policy.Arn --output text)
    fi

    # Attach the policy to the role
    echo "Attaching $policyName to role..."
    awsFunc iam attach-role-policy --role-name $roleName --policy-arn $policyArn

    # Clean up
    rm -f $tempFilePermissionPolicy

    # Attach Basic Execution role policy
    echo "Attaching basic execution policy to role..."
    awsFunc iam attach-role-policy --role-name $roleName --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
else
    echo "IAM role already exists, skipping creation."
fi

# Check Lambda function
lambdaFunctionExists=$(IsResourceExists "Lambda function" "$functionName")

if [ "$lambdaFunctionExists" == "0" ]; then
    if [ -f "$profileServiceArtifactPath" ]; then
        read -p "Do you want to repackage the profile Lambda function? Existing zip file detected. (yes/no) (default is 'no'):" repackageProfile
        repackageProfile=${repackageProfile:-no}
    else
        repackageProfile="yes"
    fi

    if [ "$repackageProfile" == "yes" ]; then
        docker build -t profileapi-builder -f ./scripts/Dockerfile.ProfileApi .
        docker run --rm -v "$(pwd)/artifacts:/app/artifacts" profileapi-builder /bin/sh -c \
            "dotnet publish ./src/LocalStack.Services.ProfileApi \
        --output ./src/LocalStack.Services.ProfileApi/bin/Release/net7.0/publish \
        --configuration 'Release' \
        --framework 'net7.0' \
        --self-contained true \
        /p:GenerateRuntimeConfigurationFiles=true \
        --runtime linux-x64 \
        /p:StripSymbols=true && \
    cd ./src/LocalStack.Services.ProfileApi/bin/Release/net7.0/publish && \
    zip -r /app/artifacts/profile-service.zip ."
        docker rmi profileapi-builder
    fi

    echo "Creating Lambda function..."
    roleArn=$(awsFunc iam get-role --role-name $roleName --query Role.Arn --output text)
    awsFunc lambda create-function --function-name $functionName --zip-file fileb://$profileServiceArtifactPath --handler bootstrap --runtime provided.al2 --role $roleArn --environment Variables="{DOTNET_ENVIRONMENT=$lambdaDotNetEnv}" --memory-size 256 --timeout 30
else
    read -p "Lambda function already exists. Do you want to update it? (yes/no):" update
    if [ "$update" == "yes" ]; then
        if [ -f "$profileServiceArtifactPath" ]; then
            read -p "Do you want to repackage the profile Lambda function? Existing zip file detected. (yes/no) (default is 'no'):" repackageProfile
            repackageProfile=${repackageProfile:-no}
        else
            repackageProfile="yes"
        fi

        if [ "$repackageProfile" == "yes" ]; then
            docker build -t profileapi-builder -f ./scripts/Dockerfile.ProfileApi .
            docker run --rm -v "$(pwd)/artifacts:/app/artifacts" profileapi-builder /bin/sh -c \
                "dotnet publish ./src/LocalStack.Services.ProfileApi \
        --output ./src/LocalStack.Services.ProfileApi/bin/Release/net7.0/publish \
        --configuration 'Release' \
        --framework 'net7.0' \
        --self-contained true \
        /p:GenerateRuntimeConfigurationFiles=true \
        --runtime linux-x64 \
        /p:StripSymbols=true && \
    cd ./src/LocalStack.Services.ProfileApi/bin/Release/net7.0/publish && \
    zip -r /app/artifacts/profile-service.zip ."
            docker rmi profileapi-builder
        fi

        echo "Updating Lambda function..."
        awsFunc lambda update-function-code --function-name $functionName --zip-file fileb://$profileServiceArtifactPath
    else
        echo "Skipping update of Lambda function."
    fi
fi

# Check message handler Lambda function
messageHandlerFunctionExists=$(IsResourceExists "Lambda function (message handler)" "$messageHandlerFunctionName")

if [ "$messageHandlerFunctionExists" == "0" ]; then
    if [ -f "$messageHandlerServiceArtifactPath" ]; then
        read -p "Do you want to repackage the message handler Lambda function? Existing zip file detected. (yes/no) (default is 'no'):" repackageMessageHandler
        repackageMessageHandler=${repackageMessageHandler:-no}
    else
        repackageMessageHandler="yes"
    fi

    if [ "$repackageMessageHandler" == "yes" ]; then
        echo "Packaging message handler Lambda function..."
        dotnet lambda package --project-location $messageHandlerServicePath --output-package $messageHandlerServiceArtifactPath
    fi

    echo "Creating message handler Lambda function..."
    roleArn=$(awsFunc iam get-role --role-name $roleName --query Role.Arn --output text)
    awsFunc lambda create-function --function-name $messageHandlerFunctionName --zip-file fileb://$messageHandlerServiceArtifactPath --handler "LocalStack.Services.MessageHandler::LocalStack.Services.MessageHandler.Function::FunctionHandler" --runtime dotnet6 --role $roleArn --environment Variables="{DOTNET_ENVIRONMENT=$lambdaDotNetEnv}" --memory-size 256 --timeout 30

    # Link SQS queue to the Lambda function
    queueUrl=$(awsFunc sqs get-queue-url --queue-name $queueName --query QueueUrl --output text)
    queueArn=$(awsFunc sqs get-queue-attributes --queue-url $queueUrl --attribute-names QueueArn --query Attributes.QueueArn --output text)

    existingMappings=$(awsFunc lambda list-event-source-mappings --function-name $messageHandlerFunctionName | jq -r ".EventSourceMappings[] | select(.EventSourceArn == \"$queueArn\") | .UUID")

    if [ -z "$existingMappings" ]; then
        echo "No existing event source mapping found."
    else
        for mappingUUID in $existingMappings; do
            echo "Deleting event source mapping with UUID: $mappingUUID..."
            awsFunc lambda delete-event-source-mapping --uuid $mappingUUID

            # Wait for 5 seconds
            echo "Waiting for 5 seconds..."
            sleep 5
        done
    fi

    awsFunc lambda create-event-source-mapping --event-source-arn $queueArn --function-name $messageHandlerFunctionName --batch-size 5
else
    read -p "Message handler Lambda function already exists. Do you want to update it? (yes/no):" update
    if [ "$update" == "yes" ]; then
        if [ -f "$messageHandlerServiceArtifactPath" ]; then
            read -p "Do you want to repackage the message handler Lambda function? Existing zip file detected. (yes/no) (default is 'no'):" repackageMessageHandler
            repackageMessageHandler=${repackageMessageHandler:-no}
        else
            repackageMessageHandler="yes"
        fi

        if [ "$repackageMessageHandler" == "yes" ]; then
            echo "Packaging message handler Lambda function..."
            dotnet lambda package --project-location $messageHandlerServicePath --output-package $messageHandlerServiceArtifactPath
        fi

        echo "Updating message handler Lambda function..."
        awsFunc lambda update-function-code --function-name $messageHandlerFunctionName --zip-file fileb://$messageHandlerServiceArtifactPath
    else
        echo "Skipping update of message handler Lambda function."
    fi
fi

echo "Setup complete!"
