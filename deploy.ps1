$env:AWS_PAGER = ""

# Prompt user for deployment target
$target = Read-Host -Prompt "Enter 'aws' for AWS or 'localstack' for LocalStack (default is 'localstack')"
if (-not $target) { $target = 'localstack' }

if ($target -eq "aws") {
    $profileName = Read-Host -Prompt "Enter AWS profile name"
    if (-not $profileName) {
        Write-Host "Please provide a valid AWS profile name."
        return
    }
    function awsFunc { aws @args --profile $profileName }
}
else {
    function awsFunc { awslocal @args }
}

# Prompt the user for AWS region
$enteredRegion = Read-Host -Prompt "Enter AWS region (default is 'eu-central-1')"
$defaultRegion = if (-not $enteredRegion) { "eu-central-1" } else { $enteredRegion }

$lambdaDotNetEnv = "Production"

if ($target -eq "localstack") {
    $lambdaDotNetEnv = "Development"
} 

# Fetch AWS account ID
$accountID = awsFunc sts get-caller-identity --query Account --output text

# Check if the command succeeded
if (-not $accountID) {
    Write-Host "Failed to fetch AWS account ID. Exiting script."
    return
}

# Known project location and artifact path
$profileServicePath = ".\src\LocalStack.Services.ProfileApi\"
$profileServiceArtifactPath = ".\artifacts\profile-service.zip"

# Known project location and artifact path for the message handler
$messageHandlerServicePath = ".\src\LocalStack.Services.MessageHandler\"
$messageHandlerServiceArtifactPath = ".\artifacts\message-handler-service.zip"

# Derived resource names
$functionName = "profile-service-demo"
$roleName = "${functionName}-role"
$policyName = "${functionName}-policy"
$bucketName = "${functionName}-bucket"
$tableName = "${functionName}-table"
$queueName = "${functionName}-queue"

# Derived resource names for the message handler
$messageHandlerFunctionName = "message-handler-demo"
$messagesTableName = "${messageHandlerFunctionName}-table"

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

        # Delete the message handler Lambda function
        Write-Host "Deleting message handler Lambda function..."
        awsFunc lambda delete-function --function-name $messageHandlerFunctionName

        # Get a list of all policies attached to the role
        $policies = awsFunc iam list-attached-role-policies --role-name $roleName --query 'AttachedPolicies[].PolicyArn' | ConvertFrom-Json

        # Detach each policy
        foreach ($policyArn in $policies) {
            Write-Host "Detaching policy $policyArn from role $roleName..."
            awsFunc iam detach-role-policy --role-name $roleName --policy-arn $policyArn

            # Only delete customer-managed policies
            if ($policyArn -like "*:iam::${accountID}:policy/*") { 
                # Delete the policy
                Write-Host "Deleting customer-managed policy: $policyArn"
                awsFunc iam delete-policy --policy-arn $policyArn
            }
        }
        
        Write-Host "Deleting IAM role..."
        awsFunc iam delete-role --role-name $roleName

        # Delete the SQS queue
        Write-Host "Deleting SQS queue..."
        $queueUrl = awsFunc sqs get-queue-url --queue-name $queueName --query QueueUrl --output text
        $queueAttributes = awsFunc sqs get-queue-attributes --queue-url $queueUrl --attribute-names QueueArn
        $queueArn = $queueAttributes | ConvertFrom-Json | Select-Object -ExpandProperty Attributes | Select-Object -ExpandProperty QueueArn
        awsFunc sqs delete-queue --queue-url $queueUrl

        Write-Host "Deleting event source mappings..."
        $existingMappings = awsFunc lambda list-event-source-mappings --function-name message-handler-demo | ConvertFrom-Json | Select-Object -ExpandProperty EventSourceMappings | Where-Object { $_.EventSourceArn -eq $queueArn }

        foreach ($mapping in $existingMappings) {
            $mappingUUID = $mapping.UUID
            
            if ($mappingUUID) {
                Write-Host "Deleting event source mapping with UUID: $mappingUUID..."
                awsFunc lambda delete-event-source-mapping --uuid $mappingUUID
            }
        }
        

        # Delete the DynamoDB table
        Write-Host "Deleting DynamoDB table..."
        awsFunc dynamodb delete-table --table-name $tableName

        # Delete the Messages DynamoDB table
        Write-Host "Deleting Messages DynamoDB table..."
        awsFunc dynamodb delete-table --table-name $messagesTableName

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

$checkMessagesTableCommand = { awsFunc dynamodb describe-table --table-name $messagesTableName }
$messagesTableExists = IsResourceExists "DynamoDB table (messages)" $messagesTableName $checkMessagesTableCommand

if ($messagesTableExists -eq "0") {
    # Create DynamoDB table for messages
    Write-Host "Creating DynamoDB table (messages)..."
    awsFunc dynamodb create-table --table-name $messagesTableName --attribute-definitions 'AttributeName=Id,AttributeType=S' --key-schema 'AttributeName=Id,KeyType=HASH' --provisioned-throughput 'ReadCapacityUnits=5,WriteCapacityUnits=5'
}
else {
    Write-Host "DynamoDB table (messages) already exists, skipping creation."
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
                    "arn:aws:sqs:*:*:$queueName",
                    "arn:aws:dynamodb:*:*:table/$messagesTableName"
                )
            }
        )
    }    

    # Convert policy to JSON
    $permissionPolicyJson = $permissionPolicy | ConvertTo-Json -Depth 10

    # Write to a temporary file
    $tempFilePermissionPolicy = [System.IO.Path]::GetTempFileName()
    Set-Content -Path $tempFilePermissionPolicy -Value $permissionPolicyJson

    # Check if the policy exists
    $policyArnStr = "arn:aws:iam::${accountID}:policy/$policyName"
    Write-Host $policyArnStr
    $checkPolicyCommand = { awsFunc iam get-policy --policy-arn $policyArnStr }
    $policyExists = IsResourceExists "IAM policy" $policyName $checkPolicyCommand

    if ($policyExists -eq "0") {
        # Create the policy
        Write-Host "Creating IAM policy..."
        $policyArn = awsFunc iam create-policy --policy-name $policyName --policy-document file://$tempFilePermissionPolicy --query 'Policy.Arn' --output text
    }
    else {
        Write-Host "IAM policy already exists. Retrieving its ARN..."
        # $policyArn = awsFunc iam get-policy --policy-name $policyName --query Policy.Arn --output text
        $policyArn = awsFunc iam get-policy --policy-arn $policyArnStr --query Policy.Arn --output text
    }

    # Attach the policy to the role
    Write-Host "Attaching $policyName to role..."
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

    if (Test-Path $profileServiceArtifactPath) {
        $repackageProfile = Read-Host -Prompt "Do you want to repackage the profile Lambda function? Existing zip file detected. (yes/no) (default is 'no')"
        if (-not $repackageProfile) { $repackageProfile = 'no' }
    }
    else {
        $repackageProfile = "yes"
    }
    
    if ($repackageProfile -eq "yes") {
        # Package Lambda function
        Write-Host "Packaging profile Lambda function..."
        dotnet lambda package --project-location $profileServicePath --output-package $profileServiceArtifactPath
    }

    # Create Lambda function
    Write-Host "Creating Lambda function..."
    $roleArn = awsFunc iam get-role --role-name $roleName --query Role.Arn --output text
    awsFunc lambda create-function --function-name $functionName --zip-file fileb://$profileServiceArtifactPath --handler bootstrap --runtime provided.al2 --role $roleArn --environment Variables="{DOTNET_ENVIRONMENT=$lambdaDotNetEnv}" --memory-size 256 --timeout 30
}
else {
    $update = Read-Host -Prompt "Lambda function already exists. Do you want to update it? (yes/no)"
    if ($update -eq "yes") {

        if (Test-Path $profileServiceArtifactPath) {
            $repackageProfile = Read-Host -Prompt "Do you want to repackage the profile Lambda function? Existing zip file detected. (yes/no) (default is 'no')"
            if (-not $repackageProfile) { $repackageProfile = 'no' }
        }
        else {
            $repackageProfile = "yes"
        }
        
        if ($repackageProfile -eq "yes") {
            # Package Lambda function
            Write-Host "Packaging profile Lambda function..."
            dotnet lambda package --project-location $profileServicePath --output-package $profileServiceArtifactPath
        }

        # Update Lambda function
        Write-Host "Updating Lambda function..."
        awsFunc lambda update-function-code --function-name $functionName --zip-file fileb://$profileServiceArtifactPath
    }
    else {
        Write-Host "Skipping update of Lambda function."
    }
}

$checkMessageHandlerLambdaCommand = { awsFunc lambda get-function --function-name $messageHandlerFunctionName }
$messageHandlerFunctionExists = IsResourceExists "Lambda function (message handler)" $messageHandlerFunctionName $checkMessageHandlerLambdaCommand

if ($messageHandlerFunctionExists -eq "0") {
    
    if (Test-Path $messageHandlerServiceArtifactPath) {
        $repackageMessageHandler = Read-Host -Prompt "Do you want to repackage the message handler Lambda function? Existing zip file detected. (yes/no) (default is 'no')"
        if (-not $repackageMessageHandler) { $repackageMessageHandler = 'no' }
    }
    else {
        $repackageMessageHandler = "yes"
    }
    
    if ($repackageMessageHandler -eq "yes") {
        # Package message handler Lambda function
        Write-Host "Packaging message handler Lambda function..."
        dotnet lambda package --project-location $messageHandlerServicePath --output-package $messageHandlerServiceArtifactPath
    }

    # Create message handler Lambda function
    Write-Host "Creating message handler Lambda function..."
    $roleArn = awsFunc iam get-role --role-name $roleName --query Role.Arn --output text
    awsFunc lambda create-function --function-name $messageHandlerFunctionName --zip-file fileb://$messageHandlerServiceArtifactPath --handler "LocalStack.Services.MessageHandler::LocalStack.Services.MessageHandler.Function::FunctionHandler" --runtime dotnet6 --role $roleArn --environment Variables="{DOTNET_ENVIRONMENT=$lambdaDotNetEnv}" --memory-size 256 --timeout 30

    # Link SQS queue to the Lambda function
    $queueUrl = awsFunc sqs get-queue-url --queue-name $queueName --query QueueUrl --output text
    $queueAttributes = awsFunc sqs get-queue-attributes --queue-url $queueUrl --attribute-names QueueArn
    $queueArn = $queueAttributes | ConvertFrom-Json | Select-Object -ExpandProperty Attributes | Select-Object -ExpandProperty QueueArn

    $existingMappings = awsFunc lambda list-event-source-mappings --function-name $messageHandlerFunctionName | ConvertFrom-Json | Select-Object -ExpandProperty EventSourceMappings | Where-Object { $_.EventSourceArn -eq $queueArn }

    if ($existingMappings.Count -eq 0) {
        Write-Host "No existing event source mapping found."
    }
    else {
        foreach ($mapping in $existingMappings) {
            $mappingUUID = $mapping.UUID
    
            if ($mappingUUID) {
                Write-Host "Deleting event source mapping with UUID: $mappingUUID..."
                awsFunc lambda delete-event-source-mapping --uuid $mappingUUID

                # Wait for 5 seconds
                Write-Host "Waiting for 5 seconds..."
                Start-Sleep -Seconds 5
            }
        }
    }
    
    awsFunc lambda create-event-source-mapping --event-source-arn $queueArn --function-name $messageHandlerFunctionName --batch-size 5 
}
else {
    $update = Read-Host -Prompt "Message handler Lambda function already exists. Do you want to update it? (yes/no)"
    if ($update -eq "yes") {

        if (Test-Path $messageHandlerServiceArtifactPath) {
            $repackageMessageHandler = Read-Host -Prompt "Do you want to repackage the message handler Lambda function? Existing zip file detected. (yes/no) (default is 'no')"
            if (-not $repackageMessageHandler) { $repackageMessageHandler = 'no' }
        }
        else {
            $repackageMessageHandler = "yes"
        }
        
        if ($repackageMessageHandler -eq "yes") {
            # Package message handler Lambda function
            Write-Host "Packaging message handler Lambda function..."
            dotnet lambda package --project-location $messageHandlerServicePath --output-package $messageHandlerServiceArtifactPath
        }

        # Update message handler Lambda function
        Write-Host "Updating message handler Lambda function..."
        awsFunc lambda update-function-code --function-name $messageHandlerFunctionName --zip-file fileb://$messageHandlerServiceArtifactPath
    }
    else {
        Write-Host "Skipping update of message handler Lambda function."
    }
}

Write-Host "Setup complete!"