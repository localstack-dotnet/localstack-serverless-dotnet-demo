#!/bin/bash

export AWS_PAGER=""

awslocal() {
    export AWS_ACCESS_KEY_ID="test"
    export AWS_SECRET_ACCESS_KEY="test"
    export AWS_DEFAULT_REGION=${DEFAULT_REGION:-${AWS_DEFAULT_REGION:-"eu-central-1"}}
    local localstack_host=${LOCALSTACK_HOST:-"localhost"}
    local localstack_url="http://$localstack_host:4566"
    aws "$@" --endpoint-url $localstack_url
}

echo -n "Enter 'aws' for AWS or 'localstack' for LocalStack (default is 'localstack'):"
read target
if [ -z "$target" ]; then target="localstack"; fi

if [ "$target" == "localstack" ]; then
    awsFunc() { awslocal "$@"; }
else
    echo -n "Enter AWS profile name:"
    read profileName
    if [ -z "$profileName" ]; then
        echo "Please provide a valid AWS profile name."
        exit 1
    fi
    awsFunc() { aws "$@" --profile $profileName; }
fi

# Prompt the user for AWS region
read -p "Enter AWS region (default is 'eu-central-1'):" enteredRegion
defaultRegion=${enteredRegion:-"eu-central-1"}

# Prompt user for number of requests
echo -n "Enter the number of requests to perform:"
read x

# Function name
functionName="profile-service-demo"

# Response array
responses=()

# Perform x requests
for ((i = 0; i < $x; i++)); do
    # Define a random variable to decide if this request should be invalid
    isInvalidRequest=$((RANDOM % 10 < 1)) # 10% chance for an invalid request

    # Randomized properties
    name="User$((RANDOM % 999999 + 1))"
    email="${name}@example.com"
    profilePicName="profile-pic-${name}.jpg"

    # Invalidate the properties if the request should be invalid
    if [ $isInvalidRequest -eq 1 ]; then
        if [ $((RANDOM % 2)) -eq 0 ]; then name=""; fi
        if [ $((RANDOM % 2)) -eq 0 ]; then email="invalidemail"; fi
        if [ $((RANDOM % 2)) -eq 0 ]; then profilePicName=""; fi
    fi

    base64Data="/9j/4QDeRXhpZgAASUkqAAgAAAAGABIBAwABAAAAAQAAABoBBQABAAAAVgAAABsBBQABAAAAXgAAACgBAwABAAAAAgAAABMCAwABAAAAAQAAAGmHBAABAAAAZgAAAAAAAABIAAAAAQAAAEgAAAABAAAABwAAkAcABAAAADAyMTABkQcABAAAAAECAwCGkgcAFgAAAMAAAAAAoAcABAAAADAxMDABoAMAAQAAAP//AAACoAQAAQAAADIAAAADoAQAAQAAADIAAAAAAAAAQVNDSUkAAABQaWNzdW0gSUQ6IDgxOf/bAEMACAYGBwYFCAcHBwkJCAoMFA0MCwsMGRITDxQdGh8eHRocHCAkLicgIiwjHBwoNyksMDE0NDQfJzk9ODI8LjM0Mv/bAEMBCQkJDAsMGA0NGDIhHCEyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMv/CABEIADIAMgMBIgACEQEDEQH/xAAaAAACAwEBAAAAAAAAAAAAAAAABAIDBQEG/8QAFgEBAQEAAAAAAAAAAAAAAAAAAAEC/9oADAMBAAIQAxAAAAHxtcWal2yUI2NZtXlYTckzm0dv5ZXn7KyoDpNKtaVtyhLQ5N5am7Tc4ptDO5MNYlEDkASAB//EACMQAAEEAQMEAwAAAAAAAAAAAAEAAgMREgQTFAUVIUIgIjL/2gAIAQEAAQUC3QryQCq1+TvU3MKvMeNHCo9tPwVeKajGUzTPLTpJENNI9O0kuOwbxXq0PLcX5Na8ICVe9LJNnkjaJ5a35ZDypnIv+1o9OBHAYR29oA6ewDgxhdvZfCah8//EABwRAAICAgMAAAAAAAAAAAAAAAARAQIQEiAhIv/aAAgBAwEBPwEeZyisw+zaiLzD8j4//8QAGBEAAwEBAAAAAAAAAAAAAAAAAREgABD/2gAIAQIBAT8Bt8Rwr//EACgQAAECBQEHBQAAAAAAAAAAAAEAEQIDECExExIiMjNRYZEjMEGBof/aAAgBAQAGPwLC7VcJmrcLhyiIoSVaGmE6cMvhagbeRO6yzWzlNd+iALjoruPpGvpxmG91qap1MOnjmEmG47JpkwxQtimVzPxNtotMOFeaXd1zCn1T4XGPHsf/xAAfEAADAQACAgMBAAAAAAAAAAAAAREhMUFRYRBxgSD/2gAIAQEAAT8hJrrwKg0SyZ2PkoxfcORQ4JqVCdoRaNcIZPJCEfrwndfwGMdmqSs3yPypVemMFUvIoCgq2QhiPBkaIl+lK9ei33WH5E9M8SP2eBZzQ7o7p+BQpz7Ft2eDvB9sFRPVEsdSr7E75MexdmqqfaS1pehIuo7iFuU4VX6eRMYWY7PBp5H8iG/hsrP/2gAMAwEAAgADAAAAELL5PJlcAnw7T/fI/wD/xAAcEQADAAMAAwAAAAAAAAAAAAAAAREQITFRcaH/2gAIAQMBAT8QR4FhWJDQlcIr5HUkt34N1we2Hn//xAAZEQADAQEBAAAAAAAAAAAAAAAAAREhEEH/2gAIAQIBAT8QpBbys0rKGsw0EfpOLv8A/8QAIRABAAICAQUBAQEAAAAAAAAAAQARITFBUWFxkaGB8MH/2gAIAQEAAT8QAU8JY7rMEOXpkNRUF0BTGvSN94duypf1cWF2ILaArBKg2HcI9wEOPsdTcKuvsvnsFc+4kOgxU72Y0QwoZXEbu0wLSDVgLwYw/wCzXowrFimvyDFlkcgFruKbGqduSrn9li0eFxSrKFQDLwiZpHPIEutziceVraq5411g9A2sGcdfMuzm1urga16wFKkdsbmDsNRiDqd4nVwQsrx9lVCSF0uTHiVyKyAWKb1ACXFFc57wIV8mHGAjg4hVyTAyoGfUEMjAoN5P6EKgEMWrpEO2WF/kgjsPgiN8oFOlSrBWf3iajCG43V0RW9sSnLO4+5//2Q=="

    # Create the inner payload
    innerPayload=$(jq -c -n \
        --arg n "$name" \
        --arg e "$email" \
        --arg p "$profilePicName" \
        --arg b64 "$base64Data" \
        '{
"Name": $n,
"Email": $e,
"ProfilePicName": $p,
"ProfilePicBase64": $b64
}')

    # Place the raw string of the inner payload into the outer payload
    payload=$(jq -c -n \
        --arg innerPayload "$innerPayload" \
        '{
"Operation": "CreateProfile",
"Payload": $innerPayload
}')

    # Invoke the Lambda function and store the response
    responseFile="response_${i}.json"
    awsFunc lambda invoke --function-name $functionName --payload "$payload" $responseFile --log-type Tail

    # rm -f $tempFilePayload

    responseContent=$(cat $responseFile)
    responses+=("$responseContent")

    # Optionally, remove the temporary response file
    rm $responseFile
done

# Write the aggregated responses to a single JSON file
printf "%s\n" "${responses[@]}" >aggregated_responses.json

echo "Load testing complete. Responses aggregated to 'aggregated_responses.json'."
