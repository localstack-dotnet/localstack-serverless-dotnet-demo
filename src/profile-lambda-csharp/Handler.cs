using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Transfer;
using LocalStack.Client;
using LocalStack.Client.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsDotnetCsharp
{
    public class Handler
    {
        private const string AwsAccessKeyId = "test";
        private const string AwsAccessKey = "test";
        private const string AwsSessionToken = "Token";
        private const string RegionName = "eu-central-1";
        private const string LocalStackHost = "localstack-profile";
        private const string BucketName = "profile-pictures";
        private const string TableName = "Profiles";

        private readonly AmazonS3Client _awsS3Client;
        private readonly AmazonDynamoDBClient _awsDynamoDbClient;

        public Handler()
        {
            ISession session = SessionStandalone
              .Init()
              .WithSessionOptions(AwsAccessKeyId, AwsAccessKey, AwsSessionToken, RegionName)
              .WithConfig(LocalStackHost)
              .Create();

            _awsS3Client = session.CreateClient<AmazonS3Client>();
            _awsDynamoDbClient = session.CreateClient<AmazonDynamoDBClient>();
        }

        public async Task<APIGatewayProxyResponse> CreateProfileAsync(APIGatewayProxyRequest request, ILambdaContext context)
        {
            WriteVariables(context);

            string requestBody = request.Body;

            context.Logger.LogLine($"Request Body\n {requestBody}");
            AddProfileModel addProfileModel = JsonConvert.DeserializeObject<AddProfileModel>(requestBody);

            context.Logger.LogLine($"Decoding Base64 image");
            var bytes = Convert.FromBase64String(addProfileModel.ProfilePicBase64);

            var fileTransferUtility = new TransferUtility(_awsS3Client);

            using (var ms = new MemoryStream(bytes))
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

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "Created",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };

            return response;
        }

        private void WriteVariables(ILambdaContext context)
        {
            context.Logger.LogLine($"RegionName: {RegionName}");
            context.Logger.LogLine($"LocalStackHost: {LocalStackHost}");
            context.Logger.LogLine($"BucketName: {BucketName}");
            context.Logger.LogLine($"TableName: {TableName}");
        }
    }
}
