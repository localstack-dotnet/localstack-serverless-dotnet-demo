echo unmapping the lambda
if (awslocal lambda list-event-source-mappings --function-name queuereader-local-queue --event-source-arn arn:aws:sqs:eu-central-1:000000000000:myQueue | ./json "EventSourceMappings" | grep -q '\[\]') then  
	echo 'Not mapped'
else
	awslocal lambda delete-event-source-mapping --uuid  $(awslocal lambda list-event-source-mappings --function-name queuereader-local-queue --event-source-arn arn:aws:sqs:eu-central-1:000000000000:myQueue | ./json "EventSourceMappings.0.UUID")
fi

echo building the lambda...
dotnet restore
if dotnet lambda package --configuration Release --framework netcoreapp3.1 --output-package artifact/queue-lambda-csharp.zip; then
	echo "packaged..."
else
	exit 1
fi
dotnet lambda package --configuration Release --framework netcoreapp3.1 --output-package artifact/queue-lambda-csharp.zip

echo deploying the lambda...
serverless deploy --verbose --stage local

echo setting up the aws environment...
awslocal dynamodb delete-table --table-name Messages > /dev/null # delete table if it exists
awslocal dynamodb create-table --table-name Messages --attribute-definitions AttributeName=message,AttributeType=S --key-schema AttributeName=message,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 --region eu-central-1 | ./json  TableDescription | ./json -a TableName TableStatus

echo testing aws-cli invoke
awslocal lambda invoke --cli-binary-format raw-in-base64-out --payload "$(< testEscaped.data)" --function-name queuereader-local-queue response.json --log-type Tail | ./json "LogResult" | base64 --decode

echo reading the Messages table
awslocal dynamodb scan --table-name Messages | ./json "Items" 

echo recreate the table
awslocal dynamodb delete-table --table-name Messages > /dev/null # delete table if it exists
awslocal dynamodb create-table --table-name Messages --attribute-definitions AttributeName=message,AttributeType=S --key-schema AttributeName=message,KeyType=HASH --provisioned-throughput ReadCapacityUnits=5,WriteCapacityUnits=5 --region eu-central-1 | ./json  TableDescription | ./json -a TableName TableStatus

echo map the queue to the lambda
if (awslocal sqs list-queues | ./json | grep -q "myQueue") then 
    if (awslocal lambda list-event-source-mappings --function-name queuereader-local-queue --event-source-arn arn:aws:sqs:eu-central-1:000000000000:myQueue | ./json "EventSourceMappings" | grep -q '\[\]') then  
        awslocal lambda create-event-source-mapping --function-name queuereader-local-queue --batch-size 10 --event-source-arn arn:aws:sqs:eu-central-1:000000000000:myQueue
    fi
fi

