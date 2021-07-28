using Amazon.SQS;
using Amazon.SQS.Model;
using Amazon.Lambda.SQSEvents;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
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
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AwsDotnetCsharp
{
    public class QueueHandler
    {
        private const string RegionName = "eu-central-1";
        private const string LocalStackHost = "localstack-profile";
        private readonly IAmazonSQS _awsSQSClient;
        private readonly IAmazonDynamoDB _awsDynamoDbClient;
        private const string TableName = "Messages";
        //private static string SecretKey = "ignore";
        //private static string AccessKey = "ignore";
        //private static string ServiceUrl = "http://localstack-profile:4566";
        private static string QueueUrl = "http://localstack-profile:4566/0123456789/myQueue";

        public QueueHandler()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .AddEnvironmentVariables()
                .Build();

            var collection = new ServiceCollection();

            ConfigureServices(collection);

            ServiceProvider = collection.BuildServiceProvider();
            _awsSQSClient = ServiceProvider.GetRequiredService<IAmazonSQS>();
            _awsDynamoDbClient = ServiceProvider.GetRequiredService<IAmazonDynamoDB>();
        }

        private IConfiguration Configuration { get; }

        private IServiceProvider ServiceProvider { get; }

        public async Task<string> ReadMessageQueueAsync(SQSEvent sqsEvent, ILambdaContext context)
        {
            if (sqsEvent != null && sqsEvent.Records != null){
                context.Logger.LogLine($"Beginning to process {sqsEvent.Records.Count} records...");
                foreach (var record in sqsEvent.Records)
                {
                    context.Logger.LogLine($"Message ID: {record.MessageId}");
                    context.Logger.LogLine($"Event Source: {record.EventSource}");

                    context.Logger.LogLine($"Record Body:");
                    context.Logger.LogLine(record.Body);
                    await _awsDynamoDbClient.PutItemAsync(TableName, new Dictionary<string, AttributeValue>()
                    {
                        {"message", new AttributeValue(record.Body)}
                    });
                }
                context.Logger.LogLine("Processing complete.");

                return $"Processed {sqsEvent.Records.Count} records.";
            }
            context.Logger.LogLine($"Invalid parameter");
            return null;
        }

        private void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection
                .AddLocalStack(Configuration)
                .AddAwsService<IAmazonDynamoDB>()
                .AddAwsService<IAmazonSQS>();
        }

        private static void WriteVariables(ILambdaContext context)
        {
            context.Logger.LogLine($"RegionName: {RegionName}");
            context.Logger.LogLine($"LocalStackHost: {LocalStackHost}");
            context.Logger.LogLine($"TableName: {TableName}");
        }
    }
}
