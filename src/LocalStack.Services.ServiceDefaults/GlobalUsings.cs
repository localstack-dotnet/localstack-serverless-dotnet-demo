// Global using directives

global using System.Globalization;
global using System.IO;
global using Amazon.Lambda.Core;
global using AWS.Messaging.Telemetry.OpenTelemetry;
global using LocalStack.Services.ServiceDefaults.Logging;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Diagnostics.HealthChecks;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Diagnostics.HealthChecks;
global using Microsoft.Extensions.Logging;
global using OpenTelemetry;
global using OpenTelemetry.Instrumentation.AWSLambda;
global using OpenTelemetry.Metrics;
global using OpenTelemetry.Trace;
global using Serilog;
global using Serilog.Configuration;
global using Serilog.Core;
global using Serilog.Events;
global using Serilog.Formatting;
global using Serilog.Formatting.Display;
global using Serilog.Formatting.Json;
global using Serilog.Sinks.OpenTelemetry;