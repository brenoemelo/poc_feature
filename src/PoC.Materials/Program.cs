using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using FluentValidation;
using PoC.FeatureFlags.Extensions;
using PoC.Materials.API.Endpoints;
using PoC.Materials.Functions;
using PoC.Materials.Infrastructure;
using PoC.Observability.Extensions;
using PoC.Shared.Infrastructure.Extensions;
using PoC.Shared.Validators;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

var handler = Environment.GetEnvironmentVariable("_HANDLER");
if (!string.IsNullOrEmpty(handler) && handler.Contains("MaterialIngestionFunction"))
{
    var wrapper = new MaterialIngestionFunction();
    await LambdaBootstrapBuilder.Create<SQSEvent>(wrapper.FunctionHandler, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Observability (Serilog + OpenTelemetry)
// Observability (Native OTel + ILogger)
builder.Services.AddStartUpMetrics();
builder.Services.AddPoCObservability(o =>
{
    o.ServiceName = "PoC-Materials";
    o.OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
});
builder.Logging.AddPoCOTelLogging(o =>
{
    o.ServiceName = "PoC-Materials";
    o.OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
});

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

builder.Services.AddMaterialsInfrastructure(builder.Configuration);

// Feature Flags (OpenFeature + Unleash)
builder.Services.AddPoCFeatureFlags(o =>
{
    o.UnleashApiUrl = builder.Configuration["FeatureFlags:UnleashApiUrl"] ?? "http://localhost:4242/api/";
    o.UnleashApiKey = builder.Configuration["FeatureFlags:UnleashApiKey"] ?? "*:development.unleash-insecure-api-token";
    o.UnleashAppName = "Default";
});

builder.Services.AddValidatorsFromAssemblyContaining<MaterialFormulationValidator>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

app.UsePoCDefaults();

app.MapGroup("/api/v1/materials")
   .MapMaterialsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "UP", service = "PoC-Materials" }));

app.Run();

/// <summary>
/// Program entry point.
/// </summary>
public partial class Program
{
    protected Program() { }
}
