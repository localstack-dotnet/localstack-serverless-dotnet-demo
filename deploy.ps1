# Prompt user for deployment target
$target = Read-Host -Prompt "Enter 'aws' for AWS or 'localstack' for LocalStack (default is 'localstack')"
if (-not $target) { $target = 'localstack' }

if ($target -eq "localstack") {
    function awsFunc { awslocal @args }
}
else {
    function awsFunc { aws @args --profile personal }
}

$defaultRegion = "eu-central-1"
$lambdaDotNetEnv = "Production"

if ($target -eq "localstack") {
    $lambdaDotNetEnv = "Development"
} 

# Known project location and artifact path
$profileServicePath = ".\src\LocalStack.Services.ProfileApi\"
$profileServiceArtifactPath = ".\artifacts\profile-service.zip"

# Derived resource names
$functionName = "profile-service-demo"
$roleName = "${functionName}-role"
$policyName = "${functionName}-policy"
$bucketName = "${functionName}-bucket"
$tableName = "${functionName}-table"
$queueName = "${functionName}-queue"

# Prompt user for operation
$operation = Read-Host -Prompt "Enter 'deploy' to create resources or 'cleanup' to delete resources (default is 'deploy')"
if (-not $operation) { $operation = 'deploy' }

if ($operation -eq "cleanup") {
    $confirmCleanup = Read-Host -Prompt "Are you sure you want to delete all resources? (yes/no)"
    if ($confirmCleanup -eq "yes") {
        Write-Host "Cleaning up resources..."

        # Delete the Lambda function
        Write-Host "Deleting Lambda function..."
        awsFunc lambda delete-function --function-name $functionName

        # Detach Basic Execution role policy and delete the IAM role
        Write-Host "Detaching basic execution policy from role..."
        awsFunc iam detach-role-policy --role-name $roleName --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
        Write-Host "Deleting IAM role..."
        awsFunc iam delete-role --role-name $roleName

        # Delete the SQS queue
        Write-Host "Deleting SQS queue..."
        $queueUrl = awsFunc sqs get-queue-url --queue-name $queueName --query QueueUrl --output text
        awsFunc sqs delete-queue --queue-url $queueUrl

        # Delete the DynamoDB table
        Write-Host "Deleting DynamoDB table..."
        awsFunc dynamodb delete-table --table-name $tableName

        # Delete the S3 bucket (ensure it is empty first)
        Write-Host "Emptying S3 bucket..."
        awsFunc s3 rm s3://$bucketName --recursive
        Write-Host "Deleting S3 bucket..."
        awsFunc s3api delete-bucket --bucket $bucketName

        Write-Host "Cleanup complete!"
        return
    }
    else {
        Write-Host "Cleanup cancelled."
        return
    }
}

function IsResourceExists {
    param (
        [string]$resourceType,
        [string]$resourceName,
        [scriptblock]$checkCommand
    )

    Write-Host "Checking if $resourceType exists with name $resourceName..."
    & $checkCommand 2>&1

    if ($LASTEXITCODE -eq 0) {
        return "1"
    }
    else {
        Write-Host "$resourceType does not exist."
        return "0"
    }
}

$checkBucketCommand = { awsFunc s3api head-bucket --bucket $bucketName }
$bucketExists = IsResourceExists "S3 bucket" $bucketName $checkBucketCommand

if ($bucketExists -eq "0") {
    # Create S3 bucket
    Write-Host "Creating S3 bucket..."
    awsFunc s3api create-bucket --bucket $bucketName --region $defaultRegion --create-bucket-configuration LocationConstraint=$defaultRegion
}
else {
    Write-Host "S3 bucket already exists, skipping creation."
}

$checkTableCommand = { awsFunc dynamodb describe-table --table-name $tableName }
$tableExists = IsResourceExists "DynamoDB table" $tableName $checkTableCommand

if ($tableExists -eq "0") {
    # Create DynamoDB table
    Write-Host "Creating DynamoDB table..."    
    awsFunc dynamodb create-table --table-name $tableName --attribute-definitions 'AttributeName=Id,AttributeType=S' --key-schema 'AttributeName=Id,KeyType=HASH' --provisioned-throughput 'ReadCapacityUnits=5,WriteCapacityUnits=5'
}
else {
    Write-Host "DynamoDB table already exists, skipping creation."
}

$checkQueueCommand = { awsFunc sqs get-queue-url --queue-name $queueName }
$queueExists = IsResourceExists "SQS queue" $queueName $checkQueueCommand

if ($queueExists -eq "0") {
    # Create SQS queue
    Write-Host "Creating SQS queue..."
    awsFunc sqs create-queue --queue-name $queueName
}
else {
    Write-Host "SQS queue already exists, skipping creation."
}

$checkRoleCommand = { awsFunc iam get-role --role-name $roleName }
$roleExists = IsResourceExists "IAM role" $roleName $checkRoleCommand

if ($roleExists -eq "0") {
    # Create IAM role
    Write-Host "Creating IAM role..."
    $assumeRolePolicy = @{
        Version   = "2012-10-17"
        Statement = @(
            @{
                Effect    = "Allow"
                Principal = @{ Service = "lambda.amazonaws.com" }
                Action    = "sts:AssumeRole"
            }
        )
    }
    
    # Convert policy to JSON
    $assumeRolePolicyJson = $assumeRolePolicy | ConvertTo-Json -Depth 10
    
    # Write to a temporary file
    $tempFile = [System.IO.Path]::GetTempFileName()
    Set-Content -Path $tempFile -Value $assumeRolePolicyJson
    
    # Create the role using the temporary file
    awsFunc iam create-role --role-name $roleName --assume-role-policy-document file://$tempFile
    
    # Optionally, remove the temporary file
    Remove-Item -Path $tempFile

    # Define the permission policy
    $permissionPolicy = @{
        Version   = "2012-10-17"
        Statement = @(
            @{
                Effect   = "Allow"
                Action   = @("s3:*", "dynamodb:*", "sqs:*")
                Resource = @(
                    "arn:aws:s3:::$bucketName/*",
                    "arn:aws:dynamodb:*:*:table/$tableName",
                    "arn:aws:sqs:*:*:$queueName"
                )
            }
        )
    }

    # Convert policy to JSON
    $permissionPolicyJson = $permissionPolicy | ConvertTo-Json -Depth 10

    # Write to a temporary file
    $tempFilePermissionPolicy = [System.IO.Path]::GetTempFileName()
    Set-Content -Path $tempFilePermissionPolicy -Value $permissionPolicyJson

    # Create the policy using the temporary file
    $policyArn = awsFunc iam create-policy --policy-name $policyName --policy-document file://$tempFilePermissionPolicy --query 'Policy.Arn' --output text

    # Attach the policy to the role
    awsFunc iam attach-role-policy --role-name $roleName --policy-arn $policyArn

    # Optionally, remove the temporary file
    Remove-Item -Path $tempFilePermissionPolicy

    # Attach Basic Execution role policy
    Write-Host "Attaching basic execution policy to role..."
    awsFunc iam attach-role-policy --role-name $roleName --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
}
else {
    Write-Host "IAM role already exists, skipping creation."
}

$checkLambdaCommand = { awsFunc lambda get-function --function-name $functionName }
$lambdaFunctionExists = IsResourceExists "Lambda function" $functionName $checkLambdaCommand

if ($lambdaFunctionExists -eq "0") {

    # Package Lambda function
    Write-Host "Packaging Lambda function..."
    dotnet lambda package --project-location $profileServicePath --output-package $profileServiceArtifactPath

    # Create Lambda function
    Write-Host "Creating Lambda function..."
    $roleArn = awsFunc iam get-role --role-name $roleName --query Role.Arn --output text
    awsFunc lambda create-function --function-name $functionName --zip-file fileb://$profileServiceArtifactPath --handler bootstrap --runtime provided.al2 --role $roleArn --environment Variables="{DOTNET_ENVIRONMENT=$lambdaDotNetEnv}" --memory-size 256 --timeout 30
}
else {
    $update = Read-Host -Prompt "Lambda function already exists. Do you want to update it? (yes/no)"
    if ($update -eq "yes") {
        # Package Lambda function
        Write-Host "Packaging Lambda function..."
        dotnet lambda package --project-location $profileServicePath --output-package $profileServiceArtifactPath

        # Update Lambda function
        Write-Host "Updating Lambda function..."
        awsFunc lambda update-function-code --function-name $functionName --zip-file fileb://$profileServiceArtifactPath
    }
    else {
        Write-Host "Skipping update of Lambda function."
    }
}

Write-Host "Setup complete!"
