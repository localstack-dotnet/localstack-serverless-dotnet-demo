namespace LocalStack.Core.Services.Common;

public abstract record AwsFailure(string AwsResource, string Target, string Reason);

public record S3Failure(string Reason, string Target) : AwsFailure("S3", Reason, Target);

public record DynamoDbFailure(string Reason, string Target) : AwsFailure("DynamoDb", Reason, Target);

public record SqsFailure(string Reason, string Target) : AwsFailure("Sqs", Reason, Target);

public record ValidationFailed(IEnumerable<ValidationFailure> Errors)
{
    public ValidationFailed(ValidationFailure error) : this(new[] { error })
    {
    }
}
