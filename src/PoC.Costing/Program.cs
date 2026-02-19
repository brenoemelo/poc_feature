using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using FluentValidation;
using PoC.Costing.API.Endpoints;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Domain.Services;
using PoC.Costing.Functions;
using PoC.Costing.Infrastructure;
using PoC.Costing.Infrastructure.ExternalServices;
using PoC.FeatureFlags.Extensions;
using PoC.Observability.Extensions;
using PoC.Shared.Infrastructure.Extensions;
using PoC.Shared.Validators;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

var handler = Environment.GetEnvironmentVariable("_HANDLER");
if (!string.IsNullOrEmpty(handler) && handler.Contains("PriceIngestionFunction"))
{
    var wrapper = new PriceIngestionFunction();
    await LambdaBootstrapBuilder.Create<SQSEvent>(wrapper.FunctionHandler, new DefaultLambdaJsonSerializer())
        .Build()
        .RunAsync();
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Observability (Native OTel + ILogger)
builder.Services.AddStartUpMetrics(); // Ensure startup metrics are captured
builder.Services.AddPoCObservability(o =>
{
    o.ServiceName = "PoC-Costing";
    o.OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
});
builder.Logging.AddPoCOTelLogging(o =>
{
    o.ServiceName = "PoC-Costing";
    o.OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
});

// Add Custom Meter to OTel
builder.Services.AddOpenTelemetry()
   .WithMetrics(metrics => 
   {
       metrics.AddMeter(PoC.Costing.Infrastructure.BusinessMetrics.MeterName);
   });

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// Dependency Injection
builder.Services.AddCostingInfrastructure(builder.Configuration);

// Feature Flags (OpenFeature + Unleash)
builder.Services.AddPoCFeatureFlags(o =>
{
    o.UnleashApiUrl = builder.Configuration["FeatureFlags:UnleashApiUrl"] ?? "http://localhost:4242/api/";
    o.UnleashApiKey = builder.Configuration["FeatureFlags:UnleashApiKey"] ?? "*:development.unleash-insecure-api-token";
    o.UnleashAppName = "Default";
});

builder.Services.AddSingleton<PoC.Costing.Infrastructure.BusinessMetrics>();
builder.Services.AddSingleton<ICostCalculator, CostCalculator>();

builder.Services.AddHttpClient<IMaterialsClient, MaterialsClient>(client =>
{
    var materialsUrl = builder.Configuration["MATERIALS_API_URL"] ?? "http://localhost:4566/restapis/material-api/prod/_user_request_";
    client.BaseAddress = new Uri(materialsUrl);
})
.AddStandardResilienceHandler();

// Validators
builder.Services.AddValidatorsFromAssemblyContaining<ComponentPriceRequestValidator>();

// JSON Configuration
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});

var app = builder.Build();

app.UsePoCDefaults();

app.MapGroup("/api/v1/costing")
   .MapCostingEndpoints();

app.Run();

/// <summary>
/// Program entry point.
/// </summary>
public partial class Program
{
    protected Program() { }
}
