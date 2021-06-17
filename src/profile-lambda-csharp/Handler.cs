using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Transfer;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using LocalStack.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsDotnetCsharp
{
    public class Handler
    {
        private const string RegionName = "eu-central-1";
        private const string LocalStackHost = "localstack-profile";
        private const string BucketName = "profile-pictures";
        private const string TableName = "Profiles";

        private readonly IAmazonS3 _awsS3Client;
        private readonly IAmazonDynamoDB _awsDynamoDbClient;

        public Handler()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .AddEnvironmentVariables()
                .Build();

            var collection = new ServiceCollection();

            ConfigureServices(collection);

            ServiceProvider = collection.BuildServiceProvider();

            _awsS3Client = ServiceProvider.GetRequiredService<IAmazonS3>();
            _awsDynamoDbClient = ServiceProvider.GetRequiredService<IAmazonDynamoDB>();
        }

        private IConfiguration Configuration { get; }

        private IServiceProvider ServiceProvider { get; }

        public async Task<Object> CreateProfileAsync(JObject requestObject, ILambdaContext context)
        {
            WriteVariables(context);
            JToken requestBodyToken = null;
            string requestBody = "";
            if (requestObject.TryGetValue("body", out requestBodyToken)){
                // The function was called by POST
                requestBody = requestBodyToken.ToString();
            } else {
                // The function was called by aws cli 'invoke'
                requestBody = requestObject.ToString();
            }

            var addProfileModel = JsonConvert.DeserializeObject<AddProfileModel>(requestBody);

            context.Logger.LogLine($"Decoding Base64 image");
            byte[] bytes = Convert.FromBase64String(addProfileModel.ProfilePicBase64);

            var fileTransferUtility = new TransferUtility(_awsS3Client);

            await using (var ms = new MemoryStream(bytes))
            {
                context.Logger.LogLine($"Uploading {addProfileModel.ProfilePicName} to {BucketName}");
                await fileTransferUtility.UploadAsync(ms, BucketName, addProfileModel.ProfilePicName);
            }

            context.Logger.LogLine($"Adding profile to DynamoDb");
            await _awsDynamoDbClient.PutItemAsync(TableName, new Dictionary<string, AttributeValue>()
            {
                {nameof(AddProfileModel.Id), new AttributeValue(addProfileModel.Id)},
                {nameof(AddProfileModel.Name), new AttributeValue(addProfileModel.Name)},
                {nameof(AddProfileModel.Email), new AttributeValue(addProfileModel.Email)},
                {nameof(AddProfileModel.ProfilePicName), new AttributeValue(addProfileModel.ProfilePicName)}
            });
            context.Logger.LogLine($"SUCCESS!!!");
            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "Created",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };
            return response;
        }

        private void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection
                .AddLocalStack(Configuration)
                .AddAwsService<IAmazonS3>()
                .AddAwsService<IAmazonDynamoDB>();
        }

        private static void WriteVariables(ILambdaContext context)
        {
            context.Logger.LogLine($"RegionName: {RegionName}");
            context.Logger.LogLine($"LocalStackHost: {LocalStackHost}");
            context.Logger.LogLine($"BucketName: {BucketName}");
            context.Logger.LogLine($"TableName: {TableName}");
        }
    }
}
