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
using PoC.Shared.Validators;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace PoC.Costing;

public class Program
{
    protected Program() { }

    public static async Task Main()
    {
        var handler = Environment.GetEnvironmentVariable("_HANDLER");
        if (!string.IsNullOrEmpty(handler) && handler.Contains("PriceIngestionFunction"))
        {
            var wrapper = new PriceIngestionFunction();
            await LambdaBootstrapBuilder.Create<SQSEvent>(wrapper.FunctionHandler, new DefaultLambdaJsonSerializer())
                .Build()
                .RunAsync();
            return;
        }

        var builder = WebApplication.CreateBuilder();
        
        // Add Observability (Logging, Tracing, Metrics)
        builder.AddPoCObservability("PoC.Costing", "1.0.0");

        builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

        // Dependency Injection
        builder.Services.AddCostingInfrastructure(builder.Configuration);

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
    Console.WriteLine($"[CONFIG] Unleash Interval set to: {o.FetchTogglesIntervalSeconds}s");
}, builder.Configuration);

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

        app.MapGroup("/api/v1/costing")
           .MapCostingEndpoints();

        await app.RunAsync();
    }
}
