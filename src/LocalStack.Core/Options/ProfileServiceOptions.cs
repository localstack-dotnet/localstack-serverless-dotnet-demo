namespace LocalStack.Core.Options;

public class ProfileServiceOptions
{
    public required string Bucket { get; init; }

    public required string Queue { get; init; }

    public required string Table { get; init; }
}