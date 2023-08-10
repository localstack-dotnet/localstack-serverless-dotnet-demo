namespace LocalStack.Core.Contracts;

public interface IS3UrlService
{
    string GetS3Url(IAmazonS3 amazonS3, string bucket, string key);
}