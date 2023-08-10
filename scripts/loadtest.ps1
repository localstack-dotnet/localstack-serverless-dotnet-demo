# Prompt user for deployment target
$target = Read-Host -Prompt "Enter 'aws' for AWS or 'localstack' for LocalStack (default is 'localstack')"
if (-not $target) { $target = 'localstack' }

if ($target -eq "localstack") {
    function awsFunc { awslocal @args }
}
else {
    function awsFunc { aws @args --profile personnal }
}

# Number of requests
$x = Read-Host -Prompt "Enter the number of requests to perform"

# Function name
$functionName = "profile-service-demo"

# Response array
$responses = @()

# Perform x requests
for ($i = 0; $i -lt $x; $i++) {
    # Define a random variable to decide if this request should be invalid
    $isInvalidRequest = (Get-Random -Minimum 0 -Maximum 10) -lt 1 # 10% chance for an invalid request

    # Randomized properties
    $name = "User" + (Get-Random -Minimum 1 -Maximum 999999)
    $email = $name + "@example.com"
    $profilePicName = "profile-pic-" + $name + ".jpg"

    # Invalidating properties if this request should be invalid
    if ($isInvalidRequest) {
        if ((Get-Random -Minimum 0 -Maximum 2) -eq 0) { $name = $null } # Invalidate the name
        if ((Get-Random -Minimum 0 -Maximum 2) -eq 0) { $email = "invalidemail" } # Invalidate the email
        if ((Get-Random -Minimum 0 -Maximum 2) -eq 0) { $profilePicName = $null } # Invalidate the profile pic name
    }

    # Payload with randomized properties
    $payloadObject = @{
        Operation = "CreateProfile"
        Payload   = @{
            Name             = $name
            Email            = $email
            ProfilePicName   = $profilePicName
            ProfilePicBase64 = "/9j/4QDeRXhpZgAASUkqAAgAAAAGABIBAwABAAAAAQAAABoBBQABAAAAVgAAABsBBQABAAAAXgAAACgBAwABAAAAAgAAABMCAwABAAAAAQAAAGmHBAABAAAAZgAAAAAAAABIAAAAAQAAAEgAAAABAAAABwAAkAcABAAAADAyMTABkQcABAAAAAECAwCGkgcAFgAAAMAAAAAAoAcABAAAADAxMDABoAMAAQAAAP//AAACoAQAAQAAADIAAAADoAQAAQAAADIAAAAAAAAAQVNDSUkAAABQaWNzdW0gSUQ6IDgxOf/bAEMACAYGBwYFCAcHBwkJCAoMFA0MCwsMGRITDxQdGh8eHRocHCAkLicgIiwjHBwoNyksMDE0NDQfJzk9ODI8LjM0Mv/bAEMBCQkJDAsMGA0NGDIhHCEyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMv/CABEIADIAMgMBIgACEQEDEQH/xAAaAAACAwEBAAAAAAAAAAAAAAAABAIDBQEG/8QAFgEBAQEAAAAAAAAAAAAAAAAAAAEC/9oADAMBAAIQAxAAAAHxtcWal2yUI2NZtXlYTckzm0dv5ZXn7KyoDpNKtaVtyhLQ5N5am7Tc4ptDO5MNYlEDkASAB//EACMQAAEEAQMEAwAAAAAAAAAAAAEAAgMREgQTFAUVIUIgIjL/2gAIAQEAAQUC3QryQCq1+TvU3MKvMeNHCo9tPwVeKajGUzTPLTpJENNI9O0kuOwbxXq0PLcX5Na8ICVe9LJNnkjaJ5a35ZDypnIv+1o9OBHAYR29oA6ewDgxhdvZfCah8//EABwRAAICAgMAAAAAAAAAAAAAAAARAQIQEiAhIv/aAAgBAwEBPwEeZyisw+zaiLzD8j4//8QAGBEAAwEBAAAAAAAAAAAAAAAAAREgABD/2gAIAQIBAT8Bt8Rwr//EACgQAAECBQEHBQAAAAAAAAAAAAEAEQIDECExExIiMjNRYZEjMEGBof/aAAgBAQAGPwLC7VcJmrcLhyiIoSVaGmE6cMvhagbeRO6yzWzlNd+iALjoruPpGvpxmG91qap1MOnjmEmG47JpkwxQtimVzPxNtotMOFeaXd1zCn1T4XGPHsf/xAAfEAADAQACAgMBAAAAAAAAAAAAAREhMUFRYRBxgSD/2gAIAQEAAT8hJrrwKg0SyZ2PkoxfcORQ4JqVCdoRaNcIZPJCEfrwndfwGMdmqSs3yPypVemMFUvIoCgq2QhiPBkaIl+lK9ei33WH5E9M8SP2eBZzQ7o7p+BQpz7Ft2eDvB9sFRPVEsdSr7E75MexdmqqfaS1pehIuo7iFuU4VX6eRMYWY7PBp5H8iG/hsrP/2gAMAwEAAgADAAAAELL5PJlcAnw7T/fI/wD/xAAcEQADAAMAAwAAAAAAAAAAAAAAAREQITFRcaH/2gAIAQMBAT8QR4FhWJDQlcIr5HUkt34N1we2Hn//xAAZEQADAQEBAAAAAAAAAAAAAAAAAREhEEH/2gAIAQIBAT8QpBbys0rKGsw0EfpOLv8A/8QAIRABAAICAQUBAQEAAAAAAAAAAQARITFBUWFxkaGB8MH/2gAIAQEAAT8QAU8JY7rMEOXpkNRUF0BTGvSN94duypf1cWF2ILaArBKg2HcI9wEOPsdTcKuvsvnsFc+4kOgxU72Y0QwoZXEbu0wLSDVgLwYw/wCzXowrFimvyDFlkcgFruKbGqduSrn9li0eFxSrKFQDLwiZpHPIEutziceVraq5411g9A2sGcdfMuzm1urga16wFKkdsbmDsNRiDqd4nVwQsrx9lVCSF0uTHiVyKyAWKb1ACXFFc57wIV8mHGAjg4hVyTAyoGfUEMjAoN5P6EKgEMWrpEO2WF/kgjsPgiN8oFOlSrBWf3iajCG43V0RW9sSnLO4+5//2Q=="
        } | ConvertTo-Json -Compress
    }
    $payloadJson = $payloadObject | ConvertTo-Json -Compress
    $payload = [System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($payloadJson))

    # Invoke the Lambda function and store the response
    $responseFile = "response_$i.json"
    awsFunc lambda invoke --function-name $functionName --payload $payload $responseFile --log-type Tail
    $responseContent = Get-Content -Path $responseFile | ConvertFrom-Json
    $responses += $responseContent

    # Optionally, remove the temporary response file
    Remove-Item -Path $responseFile
}

# Write the aggregated responses to a single JSON file
$responses | ConvertTo-Json -Depth 10 | Set-Content -Path "aggregated_responses.json"

Write-Host "Load testing complete. Responses aggregated to 'aggregated_responses.json'."
