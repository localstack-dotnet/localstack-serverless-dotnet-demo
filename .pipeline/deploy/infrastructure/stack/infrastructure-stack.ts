import { Construct } from 'constructs';
import { RemovalPolicy, Stack, StackProps } from 'aws-cdk-lib';
import { Bucket } from 'aws-cdk-lib/aws-s3';
import { Queue } from 'aws-cdk-lib/aws-sqs';
import { References } from '../../constants';

export class InfrastructureStack extends Stack {
  constructor(scope: Construct, id: string, props?: StackProps) {
    super(scope, id, props);

    new Bucket(this, References.LambdaDeployBucket.ProfileApi, {
      bucketName: References.LambdaDeployBucket.ProfileApi,
      removalPolicy: RemovalPolicy.DESTROY,
    });

    new Bucket(this, References.LambdaDeployBucket.MessageHandler, {
      bucketName: References.LambdaDeployBucket.MessageHandler,
      removalPolicy: RemovalPolicy.DESTROY,
    });

    new Queue(this, References.SQS, {
      queueName: References.SQS,
      removalPolicy: RemovalPolicy.DESTROY,
    });
  }
};
