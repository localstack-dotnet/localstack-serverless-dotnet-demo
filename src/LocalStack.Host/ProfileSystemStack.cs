using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SQS;
using Constructs;
using Attribute = Amazon.CDK.AWS.DynamoDB.Attribute;

namespace LocalStack.Host;

internal sealed class ProfileSystemStack : Stack
{
    public IBucket ProfileBucket { get; }

    public ITable ProfilesTable { get; }

    public ITable MessagesTable { get; }

    public IQueue ProfileQueue { get; }

    public IRole DemoRole { get; }

    public ProfileSystemStack(Construct scope, string id) : base(scope, id)
    {
        // S3 bucket: profile-service-demo-bucket
        ProfileBucket = new Bucket(this, "ProfileServiceDemoBucket", new BucketProps
        {
            BucketName = "profile-service-demo-bucket",
        });

        // DynamoDB: profile-service-demo-table
        ProfilesTable = new Table(this, "ProfilesTable", new TableProps
        {
            TableName = "profile-service-demo-table",
            PartitionKey = new Attribute { Name = "Id", Type = AttributeType.STRING },
            BillingMode = BillingMode.PROVISIONED,
            ReadCapacity = 5,
            WriteCapacity = 5,
        });

        // DynamoDB: message-handler-demo-table
        MessagesTable = new Table(this, "MessagesTable", new TableProps
        {
            TableName = "message-handler-demo-table",
            PartitionKey = new Attribute { Name = "Id", Type = AttributeType.STRING },
            BillingMode = BillingMode.PROVISIONED,
            ReadCapacity = 5,
            WriteCapacity = 5,
        });

        // SQS: profile-service-demo-queue
        ProfileQueue = new Queue(this, "ProfileServiceDemoQueue", new QueueProps
        {
            QueueName = "profile-service-demo-queue",
        });

        // IAM Role + inline Policy
        DemoRole = new Role(this, "ProfileServiceDemoRole", new RoleProps
        {
            RoleName = "profile-service-demo-role",
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
        });

        DemoRole.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"));

        var policy = new Policy(this, "ProfileServiceDemoPolicy", new PolicyProps
        {
            PolicyName = "profile-service-demo-policy",
            Statements =
            [
                new PolicyStatement(new PolicyStatementProps
                {
                    Effect = Effect.ALLOW,
                    Actions = ["s3:*", "dynamodb:*", "sqs:*"],
                    Resources =
                    [
                        $"arn:aws:s3:::{ProfileBucket.BucketName}/*",
                        $"arn:aws:dynamodb:*:*:table/{ProfilesTable.TableName}",
                        $"arn:aws:dynamodb:*:*:table/{MessagesTable.TableName}",
                        ProfileQueue.QueueArn,
                    ],
                }),
            ],
        });

        policy.AttachToRole(DemoRole);
    }
}