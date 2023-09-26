using Serilog;
using Serilog.Formatting.Json;
using ILogger = Microsoft.Extensions.Logging.ILogger;

[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>))]

namespace LocalStack.Services.MessageHandler;

public class Function
{
    private static readonly string DotnetEnv = GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

    private IConfiguration Configuration { get; init; }

    private IServiceProvider ServiceProvider { get; init; }

    private ILogger<Function> Logger { get; init; }

    private IMessageService MessageService { get; init; }

    public Function()
    {
        SetEnvironmentVariable("AWS_ENDPOINT_URL", "");

        Configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true)
            .AddJsonFile($"appsettings.{DotnetEnv}.json", true, true)
            .AddEnvironmentVariables()
            .Build();

        var collection = new ServiceCollection();

        ServiceProvider = ConfigureServices(collection);

        Logger = ServiceProvider.GetRequiredService<ILogger<Function>>();
        MessageService = ServiceProvider.GetRequiredService<IMessageService>();
    }

    public async Task<SaveMessageServiceResponse[]> FunctionHandler(SQSEvent @event, ILambdaContext context)
    {
        using IDisposable? scope = Logger.BeginScope(context.AwsRequestId);

        await WriteVariables(Logger);

        var messageResults = @event.Records.Select(ProcessMessageAsync).ToList();

        var saveMessageServiceResponses = await Task.WhenAll(messageResults);

        return saveMessageServiceResponses;
    }

    private async Task<SaveMessageServiceResponse> ProcessMessageAsync(SQSEvent.SQSMessage message)
    {
        if (string.IsNullOrEmpty(message.Body))
        {
            return new SaveMessageServiceResponse("SaveMessage", "400", "Message body cannot be empty", false, null);
        }

        Logger.LogInformation("Processed message {MessageBody}", message.Body);

        SaveMessageServiceResult saveMessageServiceResult = await MessageService.SaveMessageAsync(message.Body);

        return saveMessageServiceResult.Match(
            model => new SaveMessageServiceResponse("SaveMessage", "200", "Success", true, model),
            validationFailed => new SaveMessageServiceResponse("SaveMessage", "400", validationFailed.Errors.ToJson(), false, null),
            failure => new SaveMessageServiceResponse("SaveMessage", "500", failure.Reason, false, null));
    }

    private ServiceProvider ConfigureServices(IServiceCollection serviceCollection)
    {
        // initialize serilog's logger property with valid configuration
        LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(Configuration)
            .WriteTo.Console(new JsonFormatter());

        serviceCollection
            .AddLocalStack(Configuration)
            .AddAWSServiceLocalStack<IAmazonDynamoDB>()
            .AddTransient<IMessageService, MessageService>()
            .AddValidatorsFromAssemblyContaining<ProfileServiceRequestValidator>()
            .Configure<MessageServiceOptions>(Configuration.GetSection("MessageService"))
            .AddLogging(builder => builder.AddSerilog(loggerConfiguration.CreateLogger()));
                

        return serviceCollection.BuildServiceProvider();
    }

    private async Task WriteVariables(ILogger logger, bool writeEnv = false, bool listResources = false)
    {
        MessageServiceOptions messageServiceOptions = ServiceProvider.GetRequiredService<IOptions<MessageServiceOptions>>().Value;
        LocalStackOptions localStackOptions = ServiceProvider.GetRequiredService<IOptions<LocalStackOptions>>().Value;

        logger.LogInformation("DOTNET_ENVIRONMENT: {DotnetEnv}", DotnetEnv);
        logger.LogInformation("MessageServiceOptions: {@MessageServiceOptions}", messageServiceOptions);

        if (localStackOptions.UseLocalStack)
        {
            logger.LogInformation("LocalStackOptions: {@LocalStackOptions}", localStackOptions);
        }

        if (writeEnv)
        {
            // Get all environment variables
            IDictionary environmentVariables = GetEnvironmentVariables();

            // Print them to the console
            foreach (DictionaryEntry variable in environmentVariables)
            {
                logger.LogInformation("{VariableKey}: {VariableValue}", variable.Key, variable.Value);
            }
        }

        if (listResources)
        {
            try
            {
                var amazonS3 = ServiceProvider.GetRequiredService<IAmazonS3>();
                var amazonSqs = ServiceProvider.GetRequiredService<IAmazonSQS>();

                ListQueuesResponse listQueuesResponse = await amazonSqs.ListQueuesAsync(new ListQueuesRequest());

                logger.LogInformation("Listing Queues");
                foreach (var url in listQueuesResponse.QueueUrls)
                {
                    logger.LogInformation("Queue: {QueueUrl}", url);
                }

                var amazonSqsConfig = (AmazonSQSConfig)amazonSqs.Config;

                logger.LogInformation("Region: {RegionEndpoint}", amazonSqsConfig.RegionEndpoint);
                logger.LogInformation("ServiceURL: {ServiceUrl}", amazonSqsConfig.ServiceURL);

                ListBucketsResponse listBucketsResponse = await amazonS3.ListBucketsAsync(new ListBucketsRequest());

                foreach (S3Bucket s3Bucket in listBucketsResponse.Buckets)
                {
                    logger.LogInformation("Bucket: {BucketName}", s3Bucket.BucketName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while listing resources");
            }
        }
    }
}