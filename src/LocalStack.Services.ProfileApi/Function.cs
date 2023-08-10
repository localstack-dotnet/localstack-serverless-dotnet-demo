namespace LocalStack.Services.ProfileApi;

public class Function
{
    private static readonly string DotnetEnv = GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

    private static IConfiguration Configuration { get; set; }

    private static IServiceProvider ServiceProvider { get; set; }


    [RequiresDynamicCode("Calls ProfileService.Function.ConfigureServices(IServiceCollection)")]
    [RequiresUnreferencedCode("Calls LocalStack.Services.ProfileApi.Function.ConfigureServices(IServiceCollection)")]
    private static async Task Main()
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

        Func<ProfileServiceRequest, ILambdaContext, Task<IProfileServiceResponse<ProfileModel>>> handler = FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler, new SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>())
            .Build()
            .RunAsync();
    }

    public static async Task<IProfileServiceResponse<ProfileModel>> FunctionHandler(ProfileServiceRequest profileServiceRequest, ILambdaContext context)
    {
        var profileService = ServiceProvider.GetRequiredService<IProfileService>();
        var logger = ServiceProvider.GetRequiredService<ILogger<Function>>();
        var validator = ServiceProvider.GetRequiredService<IValidator<ProfileServiceRequest>>();

        using IDisposable? scope = logger.BeginScope(context.AwsRequestId);

        await WriteVariables(profileServiceRequest, logger);

        try
        {
            ValidationResult validationResult = await validator.ValidateAsync(profileServiceRequest);

            if (!validationResult.IsValid)
            {
                return new AddProfileServiceResponse(profileServiceRequest.Operation, "400", validationResult.Errors.ToJson(), false, null);
            }

            string operation = profileServiceRequest.Operation;

            switch (operation)
            {
                case "CreateProfile":
                    AddProfileModel addProfile =
                        JsonSerializer.Deserialize(profileServiceRequest.Payload, LambdaFunctionJsonSerializerContext.Default.AddProfileModel)!;
                    ProfileServiceResult result = await profileService.CreateProfileAsync(addProfile);

                    return result.Match(
                        model => new AddProfileServiceResponse(operation, "200", "Created", true, model),
                        validationFailed => new AddProfileServiceResponse(operation, "400", validationFailed.Errors.ToJson(), false, null),
                        awsFailure => new AddProfileServiceResponse(operation, "500", awsFailure.Reason, false, null));
                case "GetProfile":
                    return null;
                default:
                    return new AddProfileServiceResponse(operation, "400", "Invalid Operation", false, null);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error in function");

            return new AddProfileServiceResponse(profileServiceRequest.Operation, "500", e.Message, false, null);
        }
    }

    [RequiresDynamicCode("Calls Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider()")]
    [RequiresUnreferencedCode("Calls Microsoft.Extensions.DependencyInjection.OptionsConfigurationServiceCollectionExtensions.Configure<TOptions>(IConfiguration)")]
    private static ServiceProvider ConfigureServices(IServiceCollection serviceCollection)
    {
        serviceCollection
            .AddLocalStack(Configuration)
            .AddAWSServiceLocalStack<IAmazonS3>()
            .AddAWSServiceLocalStack<IAmazonSQS>()
            .AddAWSServiceLocalStack<IAmazonDynamoDB>()
            .AddTransient<IProfileService, ProfileService>()
            .AddTransient<IS3UrlService, S3UrlService>()
            .AddValidatorsFromAssemblyContaining<ProfileServiceRequestValidator>()
            .AddLogging(builder => builder.AddLambdaLogger(new LambdaLoggerOptions(Configuration)))
            .Configure<ProfileServiceOptions>(Configuration.GetSection("ProfileService"));

        return serviceCollection.BuildServiceProvider();
    }

    private static async Task WriteVariables(ProfileServiceRequest profileServiceRequest, ILogger logger, bool writeEnv = false, bool writePayload = false,
        bool listResources = false)
    {
        ProfileServiceOptions profileServiceOptions = ServiceProvider.GetRequiredService<IOptions<ProfileServiceOptions>>().Value;
        LocalStackOptions localStackOptions = ServiceProvider.GetRequiredService<IOptions<LocalStackOptions>>().Value;

        logger.LogInformation("DOTNET_ENVIRONMENT: {DotnetEnv}", DotnetEnv);
        logger.LogInformation("ProfileServiceOptions: {@ProfileServiceOptions}", profileServiceOptions);

        if (localStackOptions.UseLocalStack)
        {
            logger.LogInformation("LocalStackOptions: {@LocalStackOptions}", localStackOptions);
        }

        logger.LogInformation("Operation: {Operation}", profileServiceRequest.Operation);

        if (writePayload)
        {
            logger.LogInformation("Payload: {Payload}",
                JsonSerializer.Serialize(profileServiceRequest, LambdaFunctionJsonSerializerContext.Default.ProfileServiceRequest));
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
                foreach (string url in listQueuesResponse.QueueUrls)
                {
                    logger.LogInformation("Queue: {QueueUrl}", url);
                }

                var amazonSqsConfig = (AmazonSQSConfig)amazonSqs.Config;

                logger.LogInformation("Region: {RegionEndpoint}", amazonSqsConfig.RegionEndpoint);
                logger.LogInformation("ServiceURL: {ServiceURL}", amazonSqsConfig.ServiceURL);

                ListBucketsResponse listBucketsResponse = await amazonS3.ListBucketsAsync(new ListBucketsRequest());

                foreach (S3Bucket s3Bucket in listBucketsResponse.Buckets)
                {
                    logger.LogInformation("Bucket: {BucketName}", s3Bucket.BucketName);
                }
            }
            catch(Exception ex)
            {
                logger.LogError("Error while listing resources", ex);
            }
        }
    }
}