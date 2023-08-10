namespace LocalStack.Core.Services.Profile;

public class ProfileService : IProfileService
{
    private readonly IAmazonS3 _amazonS3;
    private readonly IAmazonSQS _amazonSqs;
    private readonly IAmazonDynamoDB _amazonDynamoDb;
    private readonly IS3UrlService _s3UrlService;
    private readonly ILogger<ProfileService> _logger;
    private readonly IValidator<AddProfileModel> _addProfileModelValidator;
    private readonly ProfileServiceOptions _options;

    public ProfileService(
        IAmazonS3 amazonS3,
        IAmazonSQS amazonSqs,
        IAmazonDynamoDB amazonDynamoDb,
        IS3UrlService s3UrlService,
        ILogger<ProfileService> logger,
        IValidator<AddProfileModel> addProfileModelValidator,
        IOptions<ProfileServiceOptions> profileServiceOptions)
    {
        _amazonS3 = amazonS3;
        _amazonSqs = amazonSqs;
        _amazonDynamoDb = amazonDynamoDb;
        _s3UrlService = s3UrlService;
        _logger = logger;
        _addProfileModelValidator = addProfileModelValidator;
        _options = profileServiceOptions.Value;
    }

    public async Task<ProfileServiceResult> CreateProfileAsync(AddProfileModel addProfileModel)
    {
        ValidationResult validationResult = await _addProfileModelValidator.ValidateAsync(addProfileModel);

        if (!validationResult.IsValid)
        {
            return new ValidationFailed(validationResult.Errors);
        }

        try
        {
            byte[] bytes = Convert.FromBase64String(addProfileModel.ProfilePicBase64);

            await using var ms = new MemoryStream(bytes);
            var fileTransferUtility = new TransferUtility(_amazonS3);
            await fileTransferUtility.UploadAsync(ms, _options.Bucket, addProfileModel.ProfilePicName);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error uploading profile pic to S3");
            return new S3Failure(e.Message, _options.Bucket);
        }

        var id = Guid.NewGuid().ToString();
        string s3Url = _s3UrlService.GetS3Url(_amazonS3, _options.Bucket, addProfileModel.ProfilePicName);
        DateTime createdAt = DateTime.UtcNow;

        PutItemResponse putItemResponse = await _amazonDynamoDb.PutItemAsync(_options.Table, new Dictionary<string, AttributeValue>()
        {
            { nameof(ProfileModel.Id), new AttributeValue(id) },
            { nameof(ProfileModel.Name), new AttributeValue(addProfileModel.Name) },
            { nameof(ProfileModel.Email), new AttributeValue(addProfileModel.Email) },
            { nameof(ProfileModel.ProfilePicUrl), new AttributeValue(s3Url) },
            { nameof(ProfileModel.CreatedAt), new AttributeValue(createdAt.ToString("O")) }
        });

        if (!putItemResponse.HttpStatusCode.IsSuccessStatusCode())
        {
            return new DynamoDbFailure($"Error adding profile to DynamoDb. StatusCode:{putItemResponse.HttpStatusCode}", _options.Table);
        }

        GetQueueUrlResponse queueUrlResponse = await _amazonSqs.GetQueueUrlAsync(new GetQueueUrlRequest(_options.Queue));

        if (!queueUrlResponse.HttpStatusCode.IsSuccessStatusCode())
        {
            return new SqsFailure($"Error getting queue url.. StatusCode: {queueUrlResponse.HttpStatusCode}", _options.Queue);
        }

        var messageBody = $"Profiled created. {id}-{addProfileModel.Name}-{addProfileModel.Email}";

        var sendMessageRequest = new SendMessageRequest
        {
            QueueUrl = queueUrlResponse.QueueUrl,
            MessageBody = messageBody
        };

        SendMessageResponse sendMessageResponse = await _amazonSqs.SendMessageAsync(sendMessageRequest);

        return !sendMessageResponse.HttpStatusCode.IsSuccessStatusCode()
            ? new SqsFailure($"Error sending message to queue. StatusCode: {queueUrlResponse.HttpStatusCode}", _options.Queue)
            : new ProfileModel(id, addProfileModel.Name, addProfileModel.Email, s3Url, createdAt);
    }
}