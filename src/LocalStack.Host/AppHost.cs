#pragma warning disable CA2252 // Using 'AddAWSLambdaFunction' requires opting into preview features.

var builder = DistributedApplication.CreateBuilder(args);

// Set up a configuration for the AWS .NET SDK
var awsConfig = builder.AddAWSSDKConfig().WithRegion(RegionEndpoint.EUCentral1);

// Bootstrap the localstack container with enhanced configuration
var localstack = builder
    .AddLocalStack(awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
    });

var profileSystemStack = builder
    .AddAWSCDKStack("profile-system-stack-resource", scope => new ProfileSystemStack(scope, "profile-system-stack"))
    .WithReference(awsConfig);

profileSystemStack.AddOutput("ProfileBucketName", stack => stack.ProfileBucket.BucketName);
profileSystemStack.AddOutput("ProfilesTableName", stack => stack.ProfilesTable.TableName);
profileSystemStack.AddOutput("MessagesTableName", stack => stack.MessagesTable.TableName);
profileSystemStack.AddOutput("ProfileQueueUrl", stack => stack.ProfileQueue.QueueUrl);
profileSystemStack.AddOutput("ProfileQueueName", stack => stack.ProfileQueue.QueueName);

// Register Lambda emulators for the two projects
var profileApiLambda = builder
    .AddAWSLambdaFunction<Projects.LocalStack_Services_ProfileApi>(
        name:"ProfileApiLambda",
        lambdaHandler: "LocalStack.Services.ProfileApi::LocalStack.Services.ProfileApi.Function::FunctionHandler")
    .WithReference(profileSystemStack)
    .WithEnvironment("ProfileService:Bucket", profileSystemStack.GetOutput("ProfileBucketName"))
    .WithEnvironment("ProfileService:Queue", profileSystemStack.GetOutput("ProfileQueueName"))
    .WithEnvironment("ProfileService:Table", profileSystemStack.GetOutput("ProfilesTableName"));

var messageHandlerLambda = builder
    .AddAWSLambdaFunction<Projects.LocalStack_Services_MessageHandler>(
        name: "MessageHandlerLambda",
        lambdaHandler: "LocalStack.Services.MessageHandler::LocalStack.Services.MessageHandler.Function::FunctionHandler")
    .WithReference(profileSystemStack)
    .WithSQSEventSource(profileSystemStack.GetOutput("ProfileQueueUrl"))
    .WithEnvironment("MessageService:Table", profileSystemStack.GetOutput("ProfilesTableName"));

// Autoconfigures the LocalStack for both AWS Cloudformation and CDK resources adds LocalStack reference to all resources that uses AWS references
builder.UseLocalStack(localstack);

await builder.Build().RunAsync().ConfigureAwait(false);