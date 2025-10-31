[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>))]

namespace LocalStack.Services.MessageHandler;

public class Function
{
    private IServiceProvider ServiceProvider { get; init; }

    private IHostEnvironment HostEnvironment { get; init; }

    private ILogger<Function> Logger { get; init; }

    private IMessageService MessageService { get; init; }

    private TracerProvider TracerProvider { get; init; }

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();

        builder.Services
            .AddLocalStack(builder.Configuration)
            .AddAwsService<IAmazonDynamoDB>()
            .AddTransient<IMessageService, MessageService>()
            .AddValidatorsFromAssemblyContaining<ProfileServiceRequestValidator>()
            .Configure<MessageServiceOptions>(builder.Configuration.GetSection("MessageService"));

        var host = builder.Build();

        ServiceProvider = host.Services;
        HostEnvironment = builder.Environment;

        Logger = host.Services.GetRequiredService<ILogger<Function>>();
        MessageService = host.Services.GetRequiredService<IMessageService>();
        TracerProvider = host.Services.GetRequiredService<TracerProvider>();

        LocalStackOptions localStackOptions = host.Services.GetRequiredService<IOptions<LocalStackOptions>>().Value;

        if (localStackOptions.UseLocalStack)
        {
            SetEnvironmentVariable("AWS_ENDPOINT_URL", ""); // See the related bug https://github.com/localstack-dotnet/localstack-dotnet-client/issues/27
        }
    }

    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent @event, ILambdaContext context)
    {
        return await AWSLambdaWrapper.TraceAsync(TracerProvider, async (sqsEvent, lambdaContext) =>
        {
            using var scope = Logger.BeginScope(lambdaContext.AwsRequestId);
            using var activity = LocalStackActivitySource.ActivitySource.StartActivity(nameof(FunctionHandler));

            await WriteVariables();

            var batchItemFailures = new List<SQSBatchResponse.BatchItemFailure>();

            foreach (var record in sqsEvent.Records)
            {
                try
                {
                    var result = await ProcessMessageAsync(record);

                    if (!result.Success)
                    {
                        Logger.LogWarning("Failed to process message {MessageId}: {Status} - {Message}",
                            record.MessageId, result.Status, result.Message);

                        batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                        {
                            ItemIdentifier = record.MessageId
                        });
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Exception processing SQS message {MessageId}", record.MessageId);

                    batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure
                    {
                        ItemIdentifier = record.MessageId
                    });
                }
            }

            if (batchItemFailures.Count > 0)
            {
                Logger.LogWarning("Batch processing completed with {FailureCount} failures out of {TotalCount} messages",
                    batchItemFailures.Count, sqsEvent.Records.Count);
            }
            else
            {
                Logger.LogInformation("Successfully processed all {TotalCount} messages", sqsEvent.Records.Count);
            }

            return new SQSBatchResponse { BatchItemFailures = batchItemFailures };
        }, @event, context);
    }

    private async Task<SaveMessageServiceResponse> ProcessMessageAsync(SQSEvent.SQSMessage message)
    {
        if (string.IsNullOrEmpty(message.Body))
        {
            Logger.LogWarning("Received message with empty body, MessageId: {MessageId}", message.MessageId);
            return new SaveMessageServiceResponse("SaveMessage", "400", "Message body cannot be empty", false, null);
        }

        Logger.LogInformation("Processing message {MessageId} with body: {MessageBody}", message.MessageId, message.Body);

        var saveMessageServiceResult = await MessageService.SaveMessageAsync(message.Body);

        return saveMessageServiceResult.Match(
            model =>
            {
                Logger.LogInformation("Successfully saved message {MessageId}", message.MessageId);
                return new SaveMessageServiceResponse("SaveMessage", "200", "Success", true, model);
            },
            validationFailed =>
            {
                Logger.LogWarning("Validation failed for message {MessageId}: {Errors}", message.MessageId, validationFailed.Errors.ToJson());
                return new SaveMessageServiceResponse("SaveMessage", "400", validationFailed.Errors.ToJson(), false, null);
            },
            failure =>
            {
                Logger.LogError("Failed to save message {MessageId}: {Reason}", message.MessageId, failure.Reason);
                return new SaveMessageServiceResponse("SaveMessage", "500", failure.Reason, false, null);
            });
    }

    private async Task WriteVariables(bool writeEnv = false, bool writeLocalStackOptions = false, bool listResources = false)
    {
        var messageServiceOptions = ServiceProvider.GetRequiredService<IOptions<MessageServiceOptions>>().Value;
        var localStackOptions = ServiceProvider.GetRequiredService<IOptions<LocalStackOptions>>().Value;

        Logger.LogInformation("DOTNET_ENVIRONMENT: {DotnetEnv}", HostEnvironment.EnvironmentName);
        Logger.LogInformation("MessageServiceOptions: {@MessageServiceOptions}", messageServiceOptions);

        if (localStackOptions.UseLocalStack && writeLocalStackOptions)
        {
            Logger.LogInformation("LocalStackOptions: {@LocalStackOptions}", localStackOptions);
        }

        if (writeEnv)
        {
            // Get all environment variables
            var environmentVariables = GetEnvironmentVariables();

            // Print them to the console
            foreach (DictionaryEntry variable in environmentVariables)
            {
                Logger.LogInformation("{VariableKey}: {VariableValue}", variable.Key, variable.Value);
            }
        }

        if (listResources)
        {
            try
            {
                var amazonS3 = ServiceProvider.GetRequiredService<IAmazonS3>();
                var amazonSqs = ServiceProvider.GetRequiredService<IAmazonSQS>();

                var listQueuesResponse = await amazonSqs.ListQueuesAsync(new ListQueuesRequest());

                Logger.LogInformation("Listing Queues");
                foreach (var url in listQueuesResponse.QueueUrls)
                {
                    Logger.LogInformation("Queue: {QueueUrl}", url);
                }

                var amazonSqsConfig = (AmazonSQSConfig)amazonSqs.Config;

                Logger.LogInformation("Region: {RegionEndpoint}", amazonSqsConfig.RegionEndpoint);
                Logger.LogInformation("ServiceURL: {ServiceUrl}", amazonSqsConfig.ServiceURL);

                var listBucketsResponse = await amazonS3.ListBucketsAsync(new ListBucketsRequest());

                foreach (var s3Bucket in listBucketsResponse.Buckets)
                {
                    Logger.LogInformation("Bucket: {BucketName}", s3Bucket.BucketName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while listing resources");
            }
        }
    }
}