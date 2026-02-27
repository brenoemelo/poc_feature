using Amazon.Lambda.Core;
using System.Reflection;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using PoC.FeatureFlags.Extensions;
using PoC.Observability.Extensions;
using PoC.Shared.Extensions;
using PoC.Populator.API.Endpoints;
using PoC.Populator.Functions;
using PoC.Populator.Infrastructure;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace PoC.Populator;

public class Program
{
    protected Program() { }

    public static async Task Main()
    {
        var handler = Environment.GetEnvironmentVariable("_HANDLER");
        if (!string.IsNullOrEmpty(handler) && handler.Contains("PopulatorWorkerFunction"))
        {
            await using var wrapper = new PopulatorWorkerFunction();
            await LambdaBootstrapBuilder.Create<SQSEvent>(wrapper.FunctionHandler, new DefaultLambdaJsonSerializer())
                .Build()
                .RunAsync();
            return;
        }

        var builder = WebApplication.CreateBuilder();

        // Add Observability (Logging, Tracing, Metrics)
        builder.AddPoCObservability("PoC.Populator", "1.0.0");

        // Observability (Native OTel + ILogger)
        builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);
        builder.Services.AddProblemDetails();

        // Dependency Injection
        builder.Services.AddPopulatorInfrastructure(builder.Configuration);

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

        app.MapGroup("/api/v1/populator")
           .MapPopulatorEndpoints();

        await app.RunAsync();
    }
}
