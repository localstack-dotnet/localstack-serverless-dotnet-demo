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
dotnet add package Amazon.Lambda.SQSEvents --version 2.0.0
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

## We will eventually be creating an SQS queue and a lambda-queue mapping.  To rerun, we should remove them
```
if (awslocal sqs list-queues | ./json | grep -q "myQueue") then 
	awslocal sqs delete-queue --queue-url http://localhost:4566/000000000000/myQueue
fi
if (awslocal lambda list-event-source-mappings --function-name queuereader-local-queue --event-source-arn arn:aws:sqs:eu-central-1:000000000000:myQueue | ./json "EventSourceMappings" | grep -q '\[\]') then  
	echo 'No mappings'
else
	awslocal lambda delete-event-source-mapping --uuid  $(awslocal lambda list-event-source-mappings --function-name queuereader-local-queue --event-source-arn arn:aws:sqs:eu-central-1:000000000000:myQueue | ./json "EventSourceMappings.0.UUID")
fi
```
## You will need the rest-api-id, which you can set into an environment variable with:
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
### Testing everything together
A single script, client-test.sh, demonstrates both POST and CLI invocation:

```
chmod +x client-test.sh
./client-test.sh
```
### Adding SQS and event-mapping
You may notice that when the client-test.sh script is run, the final result is an empty array ("[]").  
A Messages table has been created to eventually demonstrate a lambda function that will
be automatically invoked each time that a a message has been added to an SQS queue. 

If you look closely at handler.c, you'll see that in addition to writing to S3 and the Profiles DynamoDB 
table, it also writes a time-stamped message to an SQS queue.  We have a separate Messages DynamoDB that is 
intended to become a repository of these messages, but at this point in our testing, it is empty.

In a separate terminal, navigate to src/queue-lambda-csharp and run the read-sqs.sh:
```
chmod +x read-sqs.sh
./read-sqs.sh
```

This script creates a new lambda from the code in QueueHandler.cs.  That code reads a message 
from the SQS queue and writes it to the Messages table.  After creating and packaging and deploying
the lambda, the read-sqs.sh manually invokes the "queuereader-local-queue" lambda using the aws 
cli and then scans the Messages table, which should now contain a message that was passed into 
the invocation.  

At the end of the script are commands to create an event-source-mapping that maps the new lambda
to the SQS queue.  If you now return to the src/profile-lambda-csharp directory and run the 
client-test.sh script, you will eventually see that each time the original lambda has queued
an SQS message, the queuing has resulted in the invocation of the new sqs lambda, which writes the message
to the Messages table.

## To remove the queue and the mapping for re-testing:
```
if (awslocal sqs list-queues | ./json | grep -q "myQueue") then 
	awslocal sqs delete-queue --queue-url http://localhost:4566/000000000000/myQueue
fi
if (awslocal lambda list-event-source-mappings --function-name queuereader-local-queue --event-source-arn arn:aws:sqs:eu-central-1:000000000000:myQueue | ./json "EventSourceMappings" | grep -q '\[\]') then  
	echo 'No mappings'
else
	awslocal lambda delete-event-source-mapping --uuid  $(awslocal lambda list-event-source-mappings --function-name queuereader-local-queue --event-source-arn arn:aws:sqs:eu-central-1:000000000000:myQueue | ./json "EventSourceMappings.0.UUID")
fi
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

## <a name="license"></a> License
Licensed under MIT, see [LICENSE](LICENSE) for the full text.
