namespace LocalStack.Core.Services.Message.Models;

[GenerateOneOf]
public partial class SaveMessageServiceResult : OneOfBase<string, ValidationFailed, DynamoDbFailure>
{
}

public record SaveMessageServiceResponse(string Operation, string Status, string? Message, bool Success, string? Model) : IServiceResponse<string>;