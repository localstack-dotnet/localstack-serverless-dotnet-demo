import { Construct } from 'constructs';
import { Duration, RemovalPolicy, Stack, StackProps } from 'aws-cdk-lib';
import { ManagedPolicy, Role, ServicePrincipal } from 'aws-cdk-lib/aws-iam';
import { Function, Code, Runtime, Architecture } from 'aws-cdk-lib/aws-lambda';
import { RetentionDays } from 'aws-cdk-lib/aws-logs';
import { Bucket } from 'aws-cdk-lib/aws-s3';
import { References } from '../../constants';
import { Queue } from 'aws-cdk-lib/aws-sqs';
import { AttributeType, Table } from 'aws-cdk-lib/aws-dynamodb';

// lambda create-function --function-name $functionName --zip-file fileb://$profileServiceArtifactPath --handler bootstrap --runtime provided.al2 --role $roleArn --environment Variables="{DOTNET_ENVIRONMENT=$lambdaDotNetEnv}" --memory-size 256 --timeout 30

const functionName = 'profile-api';
const architecture: Architecture = Architecture.ARM_64;
const runtime: Runtime = Runtime.PROVIDED_AL2;
const handler = 'bootstrap';
const memorySize = 256;
const timeout = Duration.seconds(30);

export class ProfileApiStack extends Stack {
  constructor(scope: Construct, id: string, props?: StackProps) {
    super(scope, id, props);

    const version = this.node.tryGetContext('version');
    const lambdaArtifactBucket = Bucket.fromBucketName(this, References.LambdaDeployBucket.ProfileApi, References.LambdaDeployBucket.ProfileApi);
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
      logRetention: RetentionDays.ONE_DAY,
    });

    const messageQueueArn = `arn:aws:sqs:eu-central-1:000000000000:${References.SQS}`;
    const messageQueue = Queue.fromQueueArn(this, References.SQS, messageQueueArn);
    messageQueue.grantSendMessages(lambda);

    const profileImagesBucket = new Bucket(this, 'profile-images', { bucketName: 'profile-images' });
    profileImagesBucket.grantReadWrite(lambda);

    const table = new Table(this, References.Tables.Profiles, {
      tableName: References.Tables.Profiles,
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
};
