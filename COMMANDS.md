## DEMO

### Invoke Lambda
```powershell
awslocal lambda invoke --function-name profile-service-demo --payload fileb://./scripts/testdata/profile1.json response.json --log-type Tail
```

### S3
```powershell
awslocal s3api list-buckets --region eu-central-1
awslocal s3api list-objects --bucket "profile-service-demo-bucket"
```

### S3
```powershell
awslocal dynamodb scan --table-name profile-service-demo-table
```

### SQS
```powershell
awslocal sqs get-queue-attributes --queue-url http://localhost:4566/000000000000/profile-service-demo-queue --attribute-names All
awslocal sqs receive-message --queue-url http://localhost:4566/000000000000/profile-service-demo-queue
```