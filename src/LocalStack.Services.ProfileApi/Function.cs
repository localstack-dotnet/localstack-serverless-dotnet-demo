[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<LambdaFunctionJsonSerializerContext>))]

namespace LocalStack.Services.ProfileApi;

public class Function
{
    private IServiceProvider ServiceProvider { get; init; }

    private IHostEnvironment HostEnvironment { get; init; }

    private ILogger<Function> Logger { get; init; }

    private IProfileService ProfileService { get; init; }

    private IValidator<ProfileServiceRequest> Validator { get; init; }

    private TracerProvider TracerProvider { get; init; }

    public Function()
    {
        var builder = new HostApplicationBuilder();

        builder.AddServiceDefaults();

        builder.Services
            .AddLocalStack(builder.Configuration)
            .AddAwsService<IAmazonS3>()
            .AddAwsService<IAmazonSQS>()
            .AddAwsService<IAmazonDynamoDB>()
            .AddTransient<IProfileService, ProfileService>()
            .AddTransient<IS3UrlService, S3UrlService>()
            .AddValidatorsFromAssemblyContaining<ProfileServiceRequestValidator>()
            .Configure<ProfileServiceOptions>(builder.Configuration.GetSection("ProfileService"));

        var host = builder.Build();

        ServiceProvider = host.Services;
        HostEnvironment = builder.Environment;

        Logger = host.Services.GetRequiredService<ILogger<Function>>();
        ProfileService = ServiceProvider.GetRequiredService<IProfileService>();
        Validator = ServiceProvider.GetRequiredService<IValidator<ProfileServiceRequest>>();
        TracerProvider = host.Services.GetRequiredService<TracerProvider>();

        LocalStackOptions localStackOptions = host.Services.GetRequiredService<IOptions<LocalStackOptions>>().Value;

        if (localStackOptions.UseLocalStack)
        {
            SetEnvironmentVariable("AWS_ENDPOINT_URL", ""); // See the related bug https://github.com/localstack-dotnet/localstack-dotnet-client/issues/27
        }
    }

    public async Task<IServiceResponse<ProfileModel>> FunctionHandler(ProfileServiceRequest profileServiceRequest, ILambdaContext context)
    {
        return await AWSLambdaWrapper.TraceAsync<ProfileServiceRequest, IServiceResponse<ProfileModel>>(TracerProvider, async (proxyRequest, lambdaContext) =>
        {
            using var scope = Logger.BeginScope(lambdaContext.AwsRequestId);

            await WriteVariables(proxyRequest);

            try
            {
                var validationResult = await Validator.ValidateAsync(proxyRequest);

                if (!validationResult.IsValid)
                {
                    return new AddProfileServiceResponse(proxyRequest.Operation, "400", validationResult.Errors.ToJson(), false, null);
                }

                var operation = proxyRequest.Operation;

                switch (operation)
                {
                    case "CreateProfile":
                        var addProfile = JsonSerializer.Deserialize(proxyRequest.Payload, LambdaFunctionJsonSerializerContext.Default.AddProfileModel)!;
                        var createProfileServiceResult = await ProfileService.CreateProfileAsync(addProfile);

                        return createProfileServiceResult.Match(
                            model => new AddProfileServiceResponse(operation, "200", "Created", true, model),
                            validationFailed => new AddProfileServiceResponse(operation, "400", validationFailed.Errors.ToJson(), false, null),
                            awsFailure => new AddProfileServiceResponse(operation, "500", awsFailure.Reason, false, null));
                    case "GetProfile":
                        var parsed = Guid.TryParse(proxyRequest.Payload, out var profileId);

                        if (!parsed)
                        {
                            return new GetProfileServiceResponse(operation, "400", "Invalid Profile Id", false, null);
                        }

                        var getProfileServiceResult = await ProfileService.GetProfileByIdAsync(profileId);

                        return getProfileServiceResult.Match(
                            model => new GetProfileServiceResponse(operation, "200", "Success", true, model),
                            failed => new GetProfileServiceResponse(operation, "400", failed.Errors.ToJson(), false, null),
                            _ => new GetProfileServiceResponse(operation, "404", "Not Found", false, null),
                            failure => new GetProfileServiceResponse(operation, "500", failure.Reason, false, null));
                    default:
                        return new AddProfileServiceResponse(operation, "400", "Invalid Operation", false, null);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in function");

                return new AddProfileServiceResponse(proxyRequest.Operation, "500", e.Message, false, null);
            }
        }, profileServiceRequest, context);
    }

    private async Task WriteVariables(ProfileServiceRequest profileServiceRequest, bool writeEnv = false, bool writePayload = false,
        bool listResources = false)
    {
        var profileServiceOptions = ServiceProvider.GetRequiredService<IOptions<ProfileServiceOptions>>().Value;
        var localStackOptions = ServiceProvider.GetRequiredService<IOptions<LocalStackOptions>>().Value;

        Logger.LogInformation("DOTNET_ENVIRONMENT: {DotnetEnv}", HostEnvironment.EnvironmentName);
        Logger.LogInformation("ProfileServiceOptions: {@ProfileServiceOptions}", profileServiceOptions);

        if (localStackOptions.UseLocalStack)
        {
            Logger.LogInformation("LocalStackOptions: {@LocalStackOptions}", localStackOptions);
        }

        Logger.LogInformation("Operation: {Operation}", profileServiceRequest.Operation);

        if (writePayload)
        {
            var payload = JsonSerializer.Serialize(profileServiceRequest, LambdaFunctionJsonSerializerContext.Default.ProfileServiceRequest);
            Logger.LogInformation("Payload: {Payload}", payload);
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