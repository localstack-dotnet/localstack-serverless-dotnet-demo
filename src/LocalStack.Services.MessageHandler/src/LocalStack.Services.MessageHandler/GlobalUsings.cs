// Global using directives

global using System.Collections;
global using System.Text.Json;
global using Amazon.DynamoDBv2;
global using Amazon.Lambda.Core;
global using Amazon.Lambda.Serialization.SystemTextJson;
global using Amazon.Lambda.SQSEvents;
global using Amazon.S3;
global using Amazon.S3.Model;
global using Amazon.SQS;
global using Amazon.SQS.Model;
global using FluentValidation;
global using LocalStack.Client.Extensions;
global using LocalStack.Client.Options;
global using LocalStack.Core.Contracts;
global using LocalStack.Core.Extensions;
global using LocalStack.Core.Json;
global using LocalStack.Core.Models;
global using LocalStack.Core.Options;
global using LocalStack.Core.Services.Message;
global using LocalStack.Core.Services.Message.Models;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using static System.Environment;