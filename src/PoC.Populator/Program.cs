using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using PoC.FeatureFlags.Extensions;
using PoC.Observability.Extensions;
using PoC.Populator.API.Endpoints;
using PoC.Populator.Functions;
using PoC.Populator.Infrastructure;
using PoC.Shared.Infrastructure.Extensions;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

var handler = Environment.GetEnvironmentVariable("_HANDLER");
if (!string.IsNullOrEmpty(handler) && handler.Contains("PopulatorWorkerFunction"))
{
    var wrapper = new PopulatorWorkerFunction();
    await LambdaBootstrapBuilder.Create<SQSEvent>(wrapper.FunctionHandler, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Observability (Serilog + OpenTelemetry)
builder.Services.AddPoCObservability(o =>
{
    o.ServiceName = "PoC.Populator";
    o.OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// Dependency Injection
builder.Services.AddPopulatorInfrastructure(builder.Configuration);

// Feature Flags (OpenFeature + Unleash)
builder.Services.AddPoCFeatureFlags(options =>
{
    options.UnleashApiUrl = builder.Configuration["FeatureFlags:UnleashApiUrl"] ?? "http://localhost:4242/api/";
    options.UnleashApiKey = builder.Configuration["FeatureFlags:UnleashApiKey"] ?? "default:development.unleash-insecure-api-token";
    options.UnleashAppName = "PoC-Populator";
    options.UnleashInstanceId = "populator-1";
});

// JSON Configuration
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});

var app = builder.Build();

app.UsePoCDefaults();

app.MapGroup("/api/v1/populator")
   .MapPopulatorEndpoints();

app.Run();

/// <summary>
/// Entry point for tests.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Program"/> class.
    /// </summary>
    protected Program() { }
}
