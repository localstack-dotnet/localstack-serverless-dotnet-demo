version=$1
messageHandlerServicePath="./src/LocalStack.Services.MessageHandler/"
messageHandlerServiceArtifactPath="./artifacts/message-handler-service.$version.zip"
dotnet lambda package --project-location $messageHandlerServicePath --output-package $messageHandlerServiceArtifactPath
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=eu-central-1 aws --endpoint-url=http://localhost:4566 s3 cp $messageHandlerServiceArtifactPath s3://lambda-deploy-bucket-message-handler/$version.zip