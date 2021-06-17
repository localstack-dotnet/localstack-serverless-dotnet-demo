# LocalStack, Serverless .NET CORE demo

Demo project for usage of LocalStack, Serverless and .NET Core

![Demo](https://raw.githubusercontent.com/localstack-dotnet/localstack-serverless-dotnet-demo/master/assets/architecture.png)

## Prerequisites

Docker, NodeJs, and .NET Core 3.1 must be installed on your computer.

## Deploying the application to LocalStack

First you need to install [Serverless Framework](https://serverless.com/framework/docs/providers/aws/guide/installation/)

```
npm install -g serverless
```
## Restart Docker

To be sure that Docker is running, run 
```
sudo systemctl restart docker.service
```
## Start LocalStack

At the root of the project, run:
```
docker-compose up
```
## Running the aws client

In a separate terminal session, install the `serverless-localstack` plug-in under `src/profile-lambda-csharp`. Use the commands below.
```
npm install serverless-localstack
dotnet tool install -g Amazon.Lambda.Tools
export PATH="$PATH:/home/gitpod/.dotnet/tools"
```
## Build the lambda package
The following compiles the code and packages it under the artifact subdirectory
```
dotnet restore
dotnet lambda package --configuration Release --framework netcoreapp3.1 --output-package artifact/profile-lambda-csharp.zip
```
## Deploy the lambda
The following command deploys the lambda application to the running LocalStack instance
```
serverless deploy --verbose --stage local
```
## Setting up a test environment
Before running the application, first we need create necessary resources on LocalStack.

First install [LocalStack.NET AWS CLI](https://github.com/localstack-dotnet/localstack-awscli-local) tool. This .NET Core global tool provides the `awslocal` command, which is a thin wrapper around the aws command line interface for use with LocalStack.

```
curl "https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip" -o "awscliv2.zip"
unzip awscliv2.zip
sudo ./aws/install
dotnet tool install --global LocalStack.AwsLocal
```

Using the following commands we can create the necessary resources on LocalStack.

```
awslocal s3 rb s3://profile-pictures  --force > /dev/null # delete bucket if it exists 
awslocal s3api create-bucket --bucket profile-pictures --region eu-central-1
awslocal dynamodb delete-table --table-name Profiles > /dev/null # delete table if it exists
awslocal dynamodb create-table --table-name Profiles --attribute-definitions AttributeName=Id,AttributeType=S --key-schema AttributeName=Id,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 --region eu-central-1 | ./json  TableDescription | ./json -a TableName TableStatus
```

You will need the rest-api-id, which you can set into an environment variable with:
```
export RESTAPI=$(awslocal apigateway get-rest-apis | ./json "items.0.id")
```
## Test the lambda
You can test the application using the following curl command.

```
curl -X POST "http://localhost:4566/restapis/${RESTAPI}/local/_user_request_/profile" -H 'Content-Type: application/json' --data '@test.data'
```

Using the following commands, you can check whether records are created in both S3 and DyanmoDB.

```
awslocal s3api list-objects --bucket profile-pictures
```
and 
```
awslocal dynamodb scan --table-name Profiles
```

### Handy commands:

## List all functions:
```
awslocal lambda list-functions
```
### List rest apis
```
awslocal apigateway get-rest-apis
```
# Invoke lambda from cli

```
awslocal lambda invoke --cli-binary-format raw-in-base64-out --function-name profile-local-hello --payload "$(< testEscaped.data)" response.json --log-type Tail | ./json "LogResult" | base64 --decode
```
# Delete function
`awslocal lambda delete-function --function-name profile-local-hello`
# Delete the table
`awslocal dynamodb delete-table     --table-name Profiles`
# List buckets
`awslocal s3api list-buckets`

# List objects
`awslocal s3api list-objects --bucket profile-local-serverlessdeploymentbucket-2f94a3b3`

# Get function details
`awslocal lambda get-function --function-name profile-local-hello`

# Full test of client access to lambda
chmod +x client-test.sh
client-test.sh

## <a name="license"></a> License
Licensed under MIT, see [LICENSE](LICENSE) for the full text.
