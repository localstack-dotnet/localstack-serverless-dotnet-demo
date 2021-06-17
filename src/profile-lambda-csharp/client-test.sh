echo setting path to tools and activating JSON support...
export PATH="$PATH:/home/gitpod/.dotnet/tools"
chmod +x json

echo building the lambda...
dotnet restore
dotnet lambda package --configuration Release --framework netcoreapp3.1 --output-package artifact/profile-lambda-csharp.zip

echo deploying the lambda...
serverless deploy --verbose --stage local

echo setting up the aws environment...
awslocal s3 rb s3://profile-pictures  --force > /dev/null # delete bucket if it exists 
awslocal s3api create-bucket --bucket profile-pictures --region eu-central-1
awslocal dynamodb delete-table --table-name Profiles > /dev/null # delete table if it exists
awslocal dynamodb create-table --table-name Profiles --attribute-definitions AttributeName=Id,AttributeType=S --key-schema AttributeName=Id,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 --region eu-central-1 | ./json  TableDescription | ./json -a TableName TableStatus

echo capture the rest-app id...
export RESTAPI=$(awslocal apigateway get-rest-apis | ./json "items.0.id")

echo testing POST...
curl -X POST "http://localhost:4566/restapis/${RESTAPI}/local/_user_request_/profile" -H 'Content-Type: application/json' --data '@test.data'
echo
awslocal s3api list-objects --bucket profile-pictures | ./json "Contents.Key" grep -q 'my-profile2.pic.jpg' && echo 'S3 WAS LOADED SUCCESSFULLY'
awslocal dynamodb scan --table-name Profiles | ./json "Items.0.Email.S" grep -q 'kulubettin@gmail.com' && echo 'DYNAMODB WAS LOADED SUCCESSFULLY'

echo resetting the aws environment
awslocal s3 rb s3://profile-pictures  --force > /dev/null # delete bucket if it exists 
awslocal s3api create-bucket --bucket profile-pictures --region eu-central-1
awslocal dynamodb delete-table --table-name Profiles > /dev/null # delete table if it exists
awslocal dynamodb create-table --table-name Profiles --attribute-definitions AttributeName=Id,AttributeType=S --key-schema AttributeName=Id,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 --region eu-central-1 | ./json  TableDescription | ./json -a TableName TableStatus

echo testing aws-cli invoke
awslocal lambda invoke --cli-binary-format raw-in-base64-out --function-name profile-local-hello --payload "$(< testEscaped.data)" response.json --log-type Tail | ./json "LogResult" | base64 --decode
echo
awslocal s3api list-objects --bucket profile-pictures | ./json "Contents.Key" grep -q 'my-profile2.pic.jpg' && echo 'S3 WAS LOADED SUCCESSFULLY'
awslocal dynamodb scan --table-name Profiles | ./json "Items.0.Email.S" grep -q 'kulubettin@gmail.com' && echo 'DYNAMODB WAS LOADED SUCCESSFULLY'



