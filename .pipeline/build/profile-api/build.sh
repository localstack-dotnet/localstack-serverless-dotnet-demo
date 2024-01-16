version=$1
docker build -t profileapi-builder -f ./scripts/Dockerfile.ProfileApi .
docker run --rm -v "$(pwd)/artifacts/profile-service:/app/artifacts" profileapi-builder /bin/sh -c \
    "dotnet publish ./src/LocalStack.Services.ProfileApi \
    --output ./src/LocalStack.Services.ProfileApi/bin/Release/net7.0/publish \
    --configuration 'Release' \
    --framework 'net7.0' \
    --self-contained true \
    /p:GenerateRuntimeConfigurationFiles=true \
    --runtime linux-x64 \
    /p:StripSymbols=true && \
    cd ./src/LocalStack.Services.ProfileApi/bin/Release/net7.0/publish && \
    zip -r /app/artifacts/profile-service.$version.zip ."
AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=eu-central-1 aws --endpoint-url=http://localhost:4566 s3 cp ./artifacts/profile-service/profile-service.$version.zip s3://lambda-deploy-bucket-profile-api/$version.zip
docker rmi profileapi-builder

