namespace LocalStack.Core.Services.Profile.Models;

[GenerateOneOf]
public partial class CreateProfileServiceResult : OneOfBase<ProfileModel, ValidationFailed, AwsFailure>
{
}

[GenerateOneOf]
public partial class GetProfileServiceResult : OneOfBase<ProfileModel, ValidationFailed, NotFound, DynamoDbFailure>
{
}

public record AddProfileServiceResponse(string Operation, string Status, string? Message, bool Success, ProfileModel? Model) : IServiceResponse<ProfileModel>;
    
public record GetProfileServiceResponse(string Operation, string Status, string? Message, bool Success, ProfileModel? Model) : IServiceResponse<ProfileModel>;
