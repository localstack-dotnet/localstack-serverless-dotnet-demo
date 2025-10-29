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
            .AddAWSServiceLocalStack<IAmazonDynamoDB>()
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

    public async Task<SaveMessageServiceResponse[]> FunctionHandler(SQSEvent @event, ILambdaContext context)
    {
        return await AWSLambdaWrapper.TraceAsync(TracerProvider, async (sqsEvent, lambdaContext) =>
        {
            using var scope = Logger.BeginScope(lambdaContext.AwsRequestId);

            await WriteVariables();

            var messageResults = sqsEvent.Records.Select(ProcessMessageAsync).ToList();

            var saveMessageServiceResponses = await Task.WhenAll(messageResults);

            return saveMessageServiceResponses;
        }, @event, context);
    }

    private async Task<SaveMessageServiceResponse> ProcessMessageAsync(SQSEvent.SQSMessage message)
    {
        if (string.IsNullOrEmpty(message.Body))
        {
            return new SaveMessageServiceResponse("SaveMessage", "400", "Message body cannot be empty", false, null);
        }

        Logger.LogInformation("Processed message {MessageBody}", message.Body);

        var saveMessageServiceResult = await MessageService.SaveMessageAsync(message.Body);

        return saveMessageServiceResult.Match(
            model => new SaveMessageServiceResponse("SaveMessage", "200", "Success", true, model),
            validationFailed => new SaveMessageServiceResponse("SaveMessage", "400", validationFailed.Errors.ToJson(), false, null),
            failure => new SaveMessageServiceResponse("SaveMessage", "500", failure.Reason, false, null));
    }

    private async Task WriteVariables(bool writeEnv = false, bool listResources = false)
    {
        var messageServiceOptions = ServiceProvider.GetRequiredService<IOptions<MessageServiceOptions>>().Value;
        var localStackOptions = ServiceProvider.GetRequiredService<IOptions<LocalStackOptions>>().Value;

        Logger.LogInformation("DOTNET_ENVIRONMENT: {DotnetEnv}", HostEnvironment.EnvironmentName);
        Logger.LogInformation("MessageServiceOptions: {@MessageServiceOptions}", messageServiceOptions);

        if (localStackOptions.UseLocalStack)
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