# Serverless Demo with LocalStack and .NET

This repository showcases a serverless application using .NET 6 and .NET 7 with [LocalStack](https://github.com/localstack/localstack), a fully functional local AWS cloud stack and [LocalStack.NET](https://github.com/localstack-dotnet/localstack-dotnet-client),  a thin wrapper around [aws-sdk-net](https://github.com/aws/aws-sdk-net) which automatically configures the target endpoints to use LocalStack for your local cloud application development.

## Overview

![Demo](https://raw.githubusercontent.com/localstack-dotnet/localstack-serverless-dotnet-demo/master/assets/architecture.drawio.svg)

The demo consists of two AWS Lambda functions:

1. **Profile API (.NET 7 and NativeAOT):** 
  - **Create Profile Operation:**
    - Creates a user profile in the profiles DynamoDB table.
    - Decodes and saves a base64 image from the payload to the profile images S3 Bucket.
    - Sends a success message to the messages SQS.
  - **Get Profile Operation:**
    - Retrieves the user profile from the profiles DynamoDB table.

The Profile API is developed using .NET 7 and NativeAOT. With .NET 7 Native AOT compilation, you can improve the cold-start times of your Lambda functions. To learn more about Native AOT for .NET 7, see [Using Native AOT in the Dotnet GitHub repository](https://github.com/dotnet/runtime/tree/main/src/coreclr/nativeaot#readme).

2. **Message Handler (.NET 6):**
  - Processes the success message from the messages SQS.
  - Saves the success message to the messages DynamoDB table.

The Message Handler is developed using .NET 6 as a standard AWS Lambda.

## Prerequisites

- **.NET 7**: [Download .NET 7](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
- **.NET 6**: [Download .NET 6](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
- **Amazon.Lambda.Tools (.NET global tool)**: [Amazon.Lambda.Tools on NuGet](https://www.nuget.org/packages/Amazon.Lambda.Tools/). Install using the command: `dotnet tool install --global Amazon.Lambda.Tools --version 5.8.0`. This tool allows you to pack and deploy a Lambda function from the command line in the Lambda function's project root directory. It is used by the deploy scripts.
- **Docker and docker-compose**: We use Docker to run the LocalStack container. [Install Docker](https://docs.docker.com/engine/install/) and [docker-compose](https://docs.docker.com/compose/).
- **awslocal CLI**: [awslocal CLI on GitHub](https://github.com/localstack/awscli-local). It's a thin wrapper around the AWS command line interface for use with LocalStack.
- **AWS CLI**: [Install AWS CLI](https://docs.aws.amazon.com/cli/latest/userguide/getting-started-install.html). This is used by the deploy scripts.
- **For Mac/Linux**: `jq` and `zip`

> **LocalStack** is a fully functional local AWS cloud stack. While it can be installed directly on your machine and accessed via the `localstack` CLI, the recommended approach is to run LocalStack using [Docker](https://docs.docker.com/get-docker/) or [docker-compose](https://docs.docker.com/compose/install/). For this demo, we've provided a `docker-compose` file to easily run LocalStack, but there are other methods to install and run it as well. For detailed installation and setup instructions for LocalStack, please refer to the [official LocalStack installation guide](https://docs.localstack.cloud/getting-started/installation/).

## Working with actual AWS

The entire demo application, including all the provided scripts, is designed to work seamlessly with both LocalStack and real AWS environments. When executing a script (details on scripts are provided in subsequent sections), you'll be prompted to select your deployment target. Simply choose 'aws' to deploy to your actual AWS account. Ensure you have the necessary AWS credentials and configurations set up before deploying to AWS.

## Setup and Deployment

1. **Run LocalStack**: Start LocalStack by executing the `docker-compose up` command.
2. **Deployment Scripts**: Use the deployment scripts `deploy.ps1` (for Windows) or `deploy.sh` (for Linux/Mac) to deploy the application. These scripts offer a series of prompts to guide you through the deployment process:
   - **Deployment Target**: Choose between deploying to LocalStack or AWS.
   - **AWS Profile**: If deploying to AWS, you'll be prompted to provide an AWS profile.
   - **Operation Selection**: Decide between creating (`deploy`) or deleting (`cleanup`) the AWS resources. If you opt for cleanup, you'll receive a confirmation prompt to ensure you want to delete all resources.
   - **Repackaging Lambda Functions**: If existing packaged Lambda functions are detected, you'll be asked whether you want to repackage them or use the existing packages.
   - **Lambda Function Updates**: If Lambda functions already exist, you'll be prompted to decide if you want to update them.

## Testing

### Manual Testing

You can manually test the ProfileApi using the provided JSON files located in [`scripts/testdata`](https://github.com/localstack-dotnet/localstack-serverless-dotnet-demo/tree/master/scripts/testdata). Files prefixed with `profile` contain valid payloads and will yield a success response, while those prefixed with `invalid` contain invalid payloads and will result in a bad request.

Example commands:

```bash
awslocal lambda invoke --function-name profile-service-demo --payload fileb://./scripts/testdata/profile1.json response.json --log-type Tail
awslocal lambda invoke --function-name profile-service-demo --payload fileb://./scripts/testdata/invalid1.json response.json --log-type Tail
```

The API response will be written to `response.json` file. You can extract the value of the id field from this file, update the `getprofile.json` file with this ID, and then use the following command to retrieve the saved user:

```bash
awslocal lambda invoke --function-name profile-service-demo --payload fileb://./scripts/testdata/getprofile.json response.json --log-type Tail
```

### Load Testing
Under the [`scripts`](https://github.com/localstack-dotnet/localstack-serverless-dotnet-demo/tree/master/scripts) folder, you'll find `loadtest.ps1` and `loadtest.sh`. These scripts will prompt you to choose between LocalStack or AWS for testing. They send randomly generated payloads to the Profile API. Approximately 10% of the requests are invalid, allowing you to observe the behavior of invalid requests. The results of the load tests are written to `aggregated_responses.json`.

### Verifying Resources in LocalStack
For manual testing and verification, you can use the following commands to check if the resources have been correctly created in LocalStack:

- **List all Lambdas:** `awslocal lambda list-functions`
- **List all S3 buckets:** `awslocal s3api list-buckets`
- **List all SQS queues:** `awslocal sqs list-queues`
- **List all DynamoDB tables:** `awslocal dynamodb list-tables`
- **List all items in DynamoDB:** `awslocal dynamodb scan --table-name <TABLE_NAME>`
- **List all messages in SQS:** `awslocal sqs receive-message --queue-url <QUEUE_URL>`
- **List all files in an S3 bucket:** `awslocal s3 ls s3://<BUCKET_NAME>/`
These commands are useful to ensure that the resources are set up correctly and to verify the state of your application in LocalStack.

> Notes:
> - These scripts can also be used with actual AWS. Simply replace `awslocal` with `aws` and your profile to the command `--profile <profile-name>`.
> - When conducting tests, it's beneficial to have `docker stats` running in a separate terminal. This allows you to observe the Lambda containers in action.

## Feedback and Contributions

Feel free to raise issues or submit pull requests if you find any problems or have suggestions for improvements.
