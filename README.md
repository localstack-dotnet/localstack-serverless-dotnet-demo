# LocalStack, Serverless .NET CORE demo

Demo project for usage of LocalStack, Serverless and .NET Core

![Demo](https://github.com/Blind-Striker/localstack-serverless-dotnet-demo/blob/master/assets/architecture.png?raw=true)

## Prerequisites

Docker, NodeJs, and .NET Core 2.1 must be installed on your computer.

## Deploying the application to LocalStack

First you need to install [Serverless Framework](https://serverless.com/framework/docs/providers/aws/guide/installation/)

```
npm install -g serverless
```

At the root of the project, run `docker-compose up` command at the terminal.

Then, in another terminal session, install the `serverless-localstack` plug-in under `src/profile-lambda-csharp`. Use the command below.

```
npm install serverless-localstack
```

Run `build.sh`, after that the project will be ready to deploy.

Run the command below and deploy the application to running LocalStack instance.

```
serverless deploy --verbose --stage local
```

## Runnin the application

Before running the application, first we need create necessary resources on LocalStack.

First install [LocalStack.NET AWS CLI](https://github.com/localstack-dotnet/localstack-awscli-local) tool. This .NET Core global tool provides the `awslocal` command, which is a thin wrapper around the aws command line interface for use with LocalStack.

```
dotnet tool install --global LocalStack.AwsLocal
```

Using the following commands we can create the necessary resources on LocalStack.

```
awslocal s3api create-bucket --bucket profile-pictures --region eu-central-1
```
```
awslocal dynamodb create-table --table-name Profiles --attribute-definitions AttributeName=Id,AttributeType=S --key-schema AttributeName=Id,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 --region eu-central-1
```

You can test the application using the following curl command.

```
curl -X POST \
  http://localhost:4567/restapis/442865321A-Z/local/_user_request_/profile \
  -H 'Accept: */*' \
  -H 'Accept-Encoding: gzip, deflate' \
  -H 'Cache-Control: no-cache' \
  -H 'Connection: keep-alive' \
  -H 'Content-Length: 2401' \
  -H 'Content-Type: application/json' \
  -H 'Host: localhost:4567' \
  -H 'Postman-Token: 77aa434b-ba0e-47d9-bec3-a85c6ead4747,5cb512fc-b747-4a29-8349-2d11afdc5d88' \
  -H 'User-Agent: PostmanRuntime/7.15.2' \
  -H 'cache-control: no-cache' \
  -d '{  
   "Id":"2",
   "Name":"Kulübettin",
   "Email":"kulubettin@gmail.com",
   "ProfilePicName":"my-profile2-pic.jpg",
   "ProfilePicBase64":"/9j/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wgARCAAlABkDASIAAhEBAxEB/8QAGAAAAwEBAAAAAAAAAAAAAAAAAAMFAgT/xAAYAQADAQEAAAAAAAAAAAAAAAACAwQAAf/aAAwDAQACEAMQAAAB3Fl9CzuMThZzaSuEu1cBhTzAqivPCiP/xAAgEAACAgEDBQAAAAAAAAAAAAACAwEEABITIgURFCEk/9oACAEBAAEFAnWUqxfUrCzYW+jSed5Wa1yx8SUDpjDGCKukk3rXo+WIhU1RYyWG36fNbgHysTKWvQMK24z/xAAbEQEAAgIDAAAAAAAAAAAAAAABAAIRIQMQQf/aAAgBAwEBPwHBDkxCiwqesps31//EABsRAAIBBQAAAAAAAAAAAAAAAAABEQIDEDJB/9oACAECAQE/AZY7ZI2+FO2P/8QAJBAAAgIBAwMFAQAAAAAAAAAAAQIAEQMhMUESUWEQEyIykpH/2gAIAQEABj8CofN+1xiApD8VtFrERR5m2L9wqNVudCamC5tA97mYfOkKjhiZ9Gje4GtDxAcbOKOngTqB/U2H9jYiNKqNiQ0tTA3dfT//xAAhEAACAgEEAgMAAAAAAAAAAAABEQAxIRBBYZFR8XGB8P/aAAgBAQABPyEN8NAKdEbA4MU1EafZOksLSUgMDIhBJ4ZPM4EAIDvCeUjJRr51E/QRNgRV2szN7LNmTWhNsE57dLGXHmYHHS7jDlp696H/2gAMAwEAAgADAAAAEKwOsPwf/8QAGhEBAQADAQEAAAAAAAAAAAAAAQARITFBUf/aAAgBAwEBPxA5Eyddi8hqb5RwH1Zv/8QAGBEBAQEBAQAAAAAAAAAAAAAAAQARMYH/2gAIAQIBAT8QezAwNzY/OwzxJf/EACMQAQACAgEDBAMAAAAAAAAAAAERIQAxgUFRcRBhwfGh0fD/2gAIAQEAAT8QSVwwBJ94o5eMkciooeY3zGBeccCMIjSTD4z6/wDrLSShGh6ia3jm1iHa78VOElFCSerPo8FVLIwAsw+PnEOEsGmJl5EyODRjqOj8+mBQYCYQEk92qwz5IPcEutcVlaGkYuuYKJe2f2fzhL4DDBFOJWBBVFBM7XRPthwRUW4IJfLPdc//2Q=="
}'
```

Using the following commands, you can check whether records are created in both S3 and DyanmoDB.

```
awslocal s3api list-objects --bucket profile-pictures
awslocal dynamodb scan --table-name Profiles
```

## <a name="license"></a> License
Licensed under MIT, see [LICENSE](LICENSE) for the full text.