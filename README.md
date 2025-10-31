# LocalStack Serverless .NET Demo

A reference implementation for building cloud-native serverless applications with .NET Aspire and LocalStack.

Demonstrates:

- [LocalStack.Aspire.Hosting](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack) - .NET Aspire integration for LocalStack
- [LocalStack.NET](https://github.com/localstack-dotnet/localstack-dotnet-client) v2.0.0 - AWS SDK wrapper for LocalStack
- [AWS Aspire integrations](https://github.com/aws/integrations-on-dotnet-aspire-for-aws) - Lambda emulator support

## Quick Start

```bash
git clone https://github.com/localstack-dotnet/localstack-serverless-dotnet-demo.git
cd localstack-serverless-dotnet-demo
dotnet run --project src/LocalStack.Host
```

This starts:

- LocalStack container
- AWS resource provisioning via CDK (S3, DynamoDB, SQS, IAM)
- Lambda emulators with pre-configured test requests
- Distributed tracing and logging

## Overview

![Architecture](https://raw.githubusercontent.com/localstack-dotnet/localstack-serverless-dotnet-demo/master/assets/architecture-non-transparent.drawio.png)

The demo consists of two AWS Lambda functions:

### 1. Profile API (.NET 8)

**Create Profile Operation:**

- Creates a user profile in the profiles DynamoDB table
- Decodes and saves a base64 image from the payload to the profile images S3 bucket
- Sends a success message to the messages SQS queue

**Get Profile Operation:**

- Retrieves the user profile from the profiles DynamoDB table

### 2. Message Handler (.NET 8)

- Processes success messages from the messages SQS queue
- Saves the message to the messages DynamoDB table

**Application Flow:**

1. ProfileApi validates input → Saves to S3 & DynamoDB → Sends SQS message
2. SQS triggers MessageHandler → Saves to DynamoDB
3. ProfileApi retrieves profile from DynamoDB

## What This Demonstrates

**LocalStack Integration:**

- **LocalStack.Aspire.Hosting** - Container lifecycle management and configuration
- **LocalStack.NET v2.0.0** - AWS SDK wrapper with automatic endpoint configuration
- **Lambda Emulators** - Fast local development with AWS Lambda Test Tool

**Infrastructure as Code:**

- AWS CDK .NET for resource provisioning
- S3, DynamoDB, SQS, and IAM resources
- Deployed automatically on startup

**Observability:**

- Distributed tracing with OpenTelemetry
- Structured logging with Serilog
- Health checks and metrics

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Node.js](https://nodejs.org/) (for AWS CDK)

## Local Development

### Running the Application

```bash
dotnet run --project src/LocalStack.Host
```

### Testing with AWS Lambda Test Tool

The AWS Lambda Test Tool is integrated into the Aspire Dashboard with pre-configured test requests:

1. Open Aspire Dashboard
2. Navigate to **Resources**
3. Click **Lambda Test Tool UI** to open lambda test tool
4. Select a saved request from the dropdown:
   - **CreateProfile** - Creates a profile with sample data (name, email, base64 image)
   - **GetProfileById** - Retrieves a profile (update the ID with a created profile)
5. Click **Execute Function**
6. View the response and logs

### Viewing Observability Data

**In the Aspire Dashboard:**

- **Logs**: Navigate to the **Structured** tab for each resource
- **Traces**: Click **Traces** to see end-to-end distributed tracing
- **Metrics**: View application metrics in the **Metrics** tab
- **Resources**: Monitor resource health and status

### Verifying Resources (CLI)

You can verify that resources are correctly created in LocalStack:

```bash
# List Lambda functions
aws lambda list-functions --endpoint-url http://<LOCALSTACK_HOST>

# List S3 buckets
aws s3api list-buckets --endpoint-url http://<LOCALSTACK_HOST>

# List SQS queues
aws sqs list-queues --endpoint-url http://<LOCALSTACK_HOST>

# List DynamoDB tables
aws dynamodb list-tables --endpoint-url http://<LOCALSTACK_HOST>

# Scan DynamoDB table
aws dynamodb scan --table-name profile-service-demo-table --endpoint-url http://<LOCALSTACK_HOST>

# Check SQS messages
aws sqs receive-message \
  --endpoint-url http://<LOCALSTACK_HOST> \
  --queue-url http://<LOCALSTACK_HOST>/000000000000/profile-service-demo-queue

# List S3 bucket contents
aws s3 ls s3://profile-service-demo-bucket/ --endpoint-url http://<LOCALSTACK_HOST>
```

> **Note**: You can replace `<LOCALSTACK_HOST>` with the actual LocalStack host from Aspire Dashboard.

## Roadmap

This project follows a phased development approach:

### ✅ Phase 1: Foundation (Complete)

- Aspire AppHost orchestration
- LocalStack.Aspire.Hosting integration
- CDK-based infrastructure provisioning
- Lambda emulators for fast development loop
- Distributed tracing and structured logging

### 🚧 Phase 2: Full Deployment (In Progress)

#### Part 1: Cleanup ✅

- Removed legacy deployment scripts

#### Part 2: Next Steps

- Real Lambda deployment to LocalStack/AWS
- API Gateway integration
- Blazor UI for interactive testing
- Modular CDK constructs (Core/Compute/API)

### 📋 Phase 3: Production Patterns (Planned)

- Comprehensive test suite
- CI/CD pipelines
- CloudFormation alternative
- Advanced scenarios (multi-region, EventBridge, Step Functions)

## Learn More

**Documentation:**

- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/)
- [LocalStack](https://docs.localstack.cloud/)
- [LocalStack.Aspire.Hosting](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack)
- [AWS Aspire Integrations](https://github.com/aws/integrations-on-dotnet-aspire-for-aws)
- [LocalStack.NET](https://github.com/localstack-dotnet/localstack-dotnet-client)
- [AWS CDK .NET Guide](https://docs.aws.amazon.com/cdk/v2/guide/work-with-cdk-csharp.html)

**Example Projects:**

- [LocalStack + .NET Aspire + OpenTelemetry Demo](https://github.com/Blind-Striker/dotnet-otel-aspire-localstack-demo) - Event registration system with distributed tracing
- [Lambda Playground](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/lambda) - Lambda function examples with Aspire
- [Provisioning Examples](https://github.com/localstack-dotnet/dotnet-aspire-for-localstack/tree/master/playground/provisioning) - CDK and CloudFormation provisioning patterns

## Contributing

We welcome contributions from the community! Here's how you can get involved:

- Try it out: Clone the repository and test the playground examples
- Report issues: Share bugs or feature requests via GitHub issues
- Submit improvements: Pull requests for enhancements and bug fixes

## License

MIT License - see [LICENSE](LICENSE)
