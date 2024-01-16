import { Stack, StackProps, RemovalPolicy, Duration } from 'aws-cdk-lib';
import { Table, AttributeType } from 'aws-cdk-lib/aws-dynamodb';
import { Role, ServicePrincipal, ManagedPolicy } from 'aws-cdk-lib/aws-iam';
import { Function, Architecture, Code, Runtime } from 'aws-cdk-lib/aws-lambda';
import { RetentionDays } from 'aws-cdk-lib/aws-logs';
import { Bucket } from 'aws-cdk-lib/aws-s3';
import { Queue } from 'aws-cdk-lib/aws-sqs';
import { Construct } from 'constructs';
import { References } from '../../constants';
import { SqsEventSource } from 'aws-cdk-lib/aws-lambda-event-sources';

//     awsFunc lambda create-function --function-name $messageHandlerFunctionName --zip-file fileb://$messageHandlerServiceArtifactPath --handler "LocalStack.Services.MessageHandler::LocalStack.Services.MessageHandler.Function::FunctionHandler" --runtime dotnet6 --role $roleArn --environment Variables="{DOTNET_ENVIRONMENT=$lambdaDotNetEnv}" --memory-size 256 --timeout 30

const functionName = 'message-handler';
const architecture: Architecture = Architecture.ARM_64;
const runtime: Runtime = Runtime.DOTNET_6;
const handler = 'LocalStack.Services.MessageHandler::LocalStack.Services.MessageHandler.Function::FunctionHandler';
const memorySize = 256;
const timeout = Duration.seconds(30);

export class MessageHandlerStack extends Stack {
  constructor(scope: Construct, id: string, props?: StackProps) {
    super(scope, id, props);

    const version = this.node.tryGetContext('version');
    const lambdaArtifactBucket = Bucket.fromBucketName(this, References.LambdaDeployBucket.MessageHandler, References.LambdaDeployBucket.MessageHandler);
    const artifactPath = `${version}.zip`;
    const code = Code.fromBucket(lambdaArtifactBucket, artifactPath);

    const role: Role = new Role(this, `fn-role-${functionName}`, {
      roleName: `fn-role-${functionName}`,
      assumedBy: new ServicePrincipal('lambda.amazonaws.com'),
      managedPolicies: [
        ManagedPolicy.fromAwsManagedPolicyName('service-role/AWSLambdaBasicExecutionRole'),
        ManagedPolicy.fromAwsManagedPolicyName('AmazonSSMReadOnlyAccess'),
      ],
    });

    let environment: { [key: string]: string } = {
      DOTNET_ENVIRONMENT: this.node.tryGetContext('lambdaDotNetEnv') || 'Development',
    };

    const lambda = new Function(this, `lambda-${functionName}`, {
      functionName,
      description: `${functionName}`,
      environment,
      code,
      role,
      runtime,
      architecture,
      handler,
      memorySize,
      timeout,
    });

    const messageQueueArn = `arn:aws:sqs:eu-central-1:000000000000:${References.SQS}`;
    const messageQueue = Queue.fromQueueArn(this, References.SQS, messageQueueArn);
    messageQueue.grantConsumeMessages(lambda);

    lambda.addEventSource(new SqsEventSource(messageQueue));

    const table = new Table(this, References.Tables.Messages, {
      tableName: References.Tables.Messages,
      partitionKey: {
        name: 'Id',
        type: AttributeType.STRING,
      },
      removalPolicy: RemovalPolicy.DESTROY,
      readCapacity: 5,
      writeCapacity: 5,
    });

    table.grantFullAccess(lambda);
  }
}
