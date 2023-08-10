namespace LocalStack.Core.Contracts;

public interface IProfileServiceResponse<TModel> where TModel : class
{
    string Operation { get; init; }

    string Status { get; init; }

    string Message { get; init; }

    bool Success { get; init; }

    TModel Model { get; init; }
}