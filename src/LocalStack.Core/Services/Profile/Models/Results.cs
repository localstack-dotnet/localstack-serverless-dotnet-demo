namespace LocalStack.Core.Services.Profile.Models;

[GenerateOneOf]
public partial class CreateProfileServiceResult : OneOfBase<ProfileModel, ValidationFailed, AwsFailure>
{
}

[GenerateOneOf]
public partial class GetProfileServiceResult : OneOfBase<ProfileModel, ValidationFailed, NotFound, DynamoDbFailure>
{
}

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

public record AddProfileServiceResponse(string Operation, string Status, string? Message, bool Success,
    ProfileModel? Model) : IProfileServiceResponse<ProfileModel>;
    
public record GetProfileServiceResponse(string Operation, string Status, string? Message, bool Success,
    ProfileModel? Model) : IProfileServiceResponse<ProfileModel>;