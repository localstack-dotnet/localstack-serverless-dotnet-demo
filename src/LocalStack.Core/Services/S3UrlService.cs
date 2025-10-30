namespace LocalStack.Core.Services;

public class S3UrlService(IOptions<LocalStackOptions> localStackOptions) : IS3UrlService
{
    private readonly LocalStackOptions _localStackOptions = localStackOptions.Value;

    public string GetS3Url(IAmazonS3 amazonS3, string bucket, string key)
    {
        if (_localStackOptions.UseLocalStack)
        {
            return $"http://{_localStackOptions.Config.LocalStackHost}:{_localStackOptions.Config.EdgePort}/{bucket}/{key}";
        }

        var awsRegion = GetEnvironmentVariable("AWS_REGION") ?? GetEnvironmentVariable("AWS_DEFAULT_REGION");

        if (string.IsNullOrWhiteSpace(awsRegion))
        {
            var amazonS3Config = (AmazonS3Config)amazonS3.Config;
            awsRegion = amazonS3Config.RegionEndpoint.SystemName ?? "us-east-1";
        }

        return $"https://{bucket}.s3.{awsRegion}.amazonaws.com/{key}";
    }
}