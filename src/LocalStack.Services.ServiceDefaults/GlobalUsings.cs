// Global using directives

global using AWS.Messaging.Telemetry.OpenTelemetry;
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
global using Serilog.Sinks.OpenTelemetry;
global using Serilog.Sinks.SystemConsole.Themes;