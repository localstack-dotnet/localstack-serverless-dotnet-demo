namespace LocalStack.Core;

public class LocalStackActivitySource
{
    public const string ActivitySourceName = "LocalStack.Core";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}