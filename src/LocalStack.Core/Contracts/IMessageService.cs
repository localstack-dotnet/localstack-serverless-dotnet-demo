namespace LocalStack.Core.Contracts;

public interface IMessageService
{
    Task<SaveMessageServiceResult> SaveMessageAsync(string message);
}