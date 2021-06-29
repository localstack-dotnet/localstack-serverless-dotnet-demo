using System;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

namespace AwsDotnetCsharp
{
    public class SqsSender 
    {
        private static string SecretKey = "ignore";
        private static string AccessKey = "ignore";
        private static string ServiceUrl = "http://localstack-profile:4566";
        private static string QueueName = "myQueue";
        private static string QueueUrl = "http://localstack-profile:4566/0123456789/myQueue";
        private ILambdaLogger logger {get; set;}
        public SqsSender(ILambdaLogger Logger){
            logger = Logger;
            var awsCreds = new BasicAWSCredentials(AccessKey, SecretKey);
            var config = new AmazonSQSConfig
            {
                //RegionEndpoint = Amazon.RegionEndpoint.EUCentral1,
                ServiceURL = ServiceUrl
            };
            var amazonSqsClient = new AmazonSQSClient(awsCreds, config);
            var listQueuesRequest = new ListQueuesRequest
            {
                QueueNamePrefix = "myQueue"
            };
            if (!Task.Run<bool>(async () => await QueueExists(amazonSqsClient, QueueName)).Result)
            {
                CreateQueueResponse createQueueResponse = 
                    Task.Run<CreateQueueResponse>(async () => await CreateQueue(amazonSqsClient, QueueName)).Result;
            }
            if (!Task.Run<bool>(async () => await QueueExists(amazonSqsClient, QueueName)).Result){
                logger.LogLine("QUEUE WAS NOT CREATED");
            }
        }
        private async Task<bool> QueueExists(AmazonSQSClient client, string queueName)
        {
            var listQueuesRequest = new ListQueuesRequest
            {
                QueueNamePrefix = queueName
            };
            ListQueuesResponse response = await client.ListQueuesAsync(listQueuesRequest);
            return response.QueueUrls.Count == 0 ? false : true;
        }
        private async Task<CreateQueueResponse> CreateQueue(AmazonSQSClient client, string queueName){
            var createQueueRequest = new CreateQueueRequest();
            createQueueRequest.QueueName = QueueName;
            return await client.CreateQueueAsync(createQueueRequest);
        }
        public async Task<string> Send(string message){
            var awsCreds = new BasicAWSCredentials(AccessKey, SecretKey);
            var config = new AmazonSQSConfig
            {
                //RegionEndpoint = Amazon.RegionEndpoint.EUCentral1,
                ServiceURL = ServiceUrl
            };
            var amazonSqsClient = new AmazonSQSClient(awsCreds, config);
            var sendMessageRequest = new SendMessageRequest
            {
                QueueUrl = QueueUrl,
                MessageBody = message
            };
            SendMessageResponse sendMessageResponse = await amazonSqsClient.SendMessageAsync(sendMessageRequest);
            logger.LogLine("Message queued");
            return sendMessageResponse.SequenceNumber;
        }
    }
}