namespace LocalStack.Core.Services.Message;

public class MessageService : IMessageService
{
    private readonly IAmazonDynamoDB _amazonDynamoDb;
    private readonly ILogger<MessageService> _logger;
    private readonly MessageServiceOptions _options;

    public MessageService(IAmazonDynamoDB amazonDynamoDb, ILogger<MessageService> logger, IOptions<MessageServiceOptions> options)
    {
        _amazonDynamoDb = amazonDynamoDb;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<SaveMessageServiceResult> SaveMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ValidationFailed(new ValidationFailure(nameof(message), "message cannot be empty"));
        }
        
        var id = Guid.NewGuid().ToString();
        DateTime createdAt = DateTime.UtcNow;

        PutItemResponse putItemResponse = await _amazonDynamoDb.PutItemAsync(_options.Table,
            new Dictionary<string, AttributeValue>()
            {
                { nameof(ProfileModel.Id), new AttributeValue(id) },
                { "Message", new AttributeValue(message) },
                { nameof(ProfileModel.CreatedAt), new AttributeValue(createdAt.ToString("O")) }
            });
        
        if (!putItemResponse.HttpStatusCode.IsSuccessStatusCode())
        {
            return new DynamoDbFailure($"Error adding profile to DynamoDb. StatusCode:{putItemResponse.HttpStatusCode}", _options.Table);
        }

        return message;
    }
}