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

// Add Observability (Logging, Tracing, Metrics)
        // builder.AddPoCObservability("PoC.Materials", "1.0.0");

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);
builder.Services.AddProblemDetails();

builder.Services.AddMaterialsInfrastructure(builder.Configuration);

// Feature Flags (OpenFeature + Unleash)
builder.Services.AddPoCFeatureFlags(o =>
{
    var section = builder.Configuration.GetSection("FeatureFlags");
    if (section.Exists())
    {
        section.Bind(o);
    }
    
    // Fallbacks if not in configuration
    if (string.IsNullOrEmpty(o.UnleashApiUrl) || o.UnleashApiUrl == "http://localhost:4242/api/")
    {
         o.UnleashApiUrl = builder.Configuration["FeatureFlags:UnleashApiUrl"] ?? "http://localhost:4242/api/";
    }
    if (string.IsNullOrEmpty(o.UnleashApiKey))
    {
         o.UnleashApiKey = builder.Configuration["FeatureFlags:UnleashApiKey"] ?? "*:development.unleash-insecure-api-token";
    }
    
    // Explicitly check for interval if default (30) is still there and config has it
    // Check Env Var FIRST to ensure override
    var intervalEnv = Environment.GetEnvironmentVariable("FeatureFlags__FetchTogglesIntervalSeconds");
    if (!string.IsNullOrEmpty(intervalEnv) && int.TryParse(intervalEnv, out var intervalVal))
    {
        o.FetchTogglesIntervalSeconds = intervalVal;
    }
    else
    {
        var intervalStr = builder.Configuration["FeatureFlags:FetchTogglesIntervalSeconds"];
        if (!string.IsNullOrEmpty(intervalStr) && int.TryParse(intervalStr, out var interval))
        {
            o.FetchTogglesIntervalSeconds = interval;
        }
    }
    // Console.WriteLine($"[CONFIG] Unleash Interval set to: {o.FetchTogglesIntervalSeconds}s");
});

builder.Services.AddValidatorsFromAssemblyContaining<MaterialFormulationValidator>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

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
