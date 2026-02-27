using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PoC.Costing.API.Endpoints;
using PoC.Costing.Configuration;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Domain.Services;
using PoC.Costing.Functions;
using PoC.Costing.Infrastructure;
using PoC.Costing.Infrastructure.ExternalServices;
using PoC.FeatureFlags.Extensions;
using PoC.Observability.Extensions;
using PoC.Shared.Extensions;
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
            await using var wrapper = new PriceIngestionFunction();
            await LambdaBootstrapBuilder.Create<SQSEvent>(wrapper.FunctionHandler, new DefaultLambdaJsonSerializer())
                .Build()
                .RunAsync();
            return;
        }

        var builder = WebApplication.CreateBuilder();
        
        // Add Observability (Logging, Tracing, Metrics)
        builder.AddPoCObservability("PoC.Costing", "1.0.0");

        builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);
        builder.Services.AddProblemDetails();

        // Dependency Injection
        builder.Services.AddCostingInfrastructure(builder.Configuration);

        // Feature Flags (OpenFeature + Unleash)
        builder.Services.AddPoCFeatureFlags(builder.Configuration, options => 
        {
            // FORCE ENABLE FAKE PROVIDER FOR LOCALSTACK
            // TODO: Investigate why configuration binding from Env Vars is failing
            if (builder.Environment.IsDevelopment())
            {
                options.UseFakeProvider = true;
            }
        });

        builder.Services.AddSingleton<PoC.Costing.Infrastructure.BusinessMetrics>();
        builder.Services.AddSingleton<ICostCalculator, CostCalculator>();

        builder.Services.AddHttpClient<IMaterialsClient, MaterialsClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<ServiceOptions>>().Value;
            var materialsUrl = opts.MaterialsApiUrl;
            
            if (!materialsUrl.EndsWith('/'))
            {
                materialsUrl += "/";
            }
            
            client.BaseAddress = new Uri(materialsUrl);
        })
        .AddStandardResilienceHandler();

        // Validators
        builder.Services.AddValidatorsFromAssemblyContaining<ComponentPriceRequestValidator>();

        // JSON Configuration
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
            options.SerializerOptions.WriteIndented = true;
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });

        var app = builder.Build();

        // Enable Observability Middleware (TraceId Injection, Flush)
        app.UsePoCObservability();

        // Middleware to fix double slashes from LocalStack/APIGW
        app.UseDoubleSlashFix();

        app.MapGroup("/api/v1/costing")
           .MapCostingEndpoints();

        await app.RunAsync();
    }
}
