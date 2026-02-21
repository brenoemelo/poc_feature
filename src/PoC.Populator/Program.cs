using Amazon.Lambda.Core;
using System.Reflection;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using PoC.FeatureFlags.Extensions;
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
        // Lê a versão injetada no build
        var assembly = Assembly.GetExecutingAssembly();
        var versionInfo = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Imprime no log da AWS/LocalStack
        Console.WriteLine($"===================================================");
        Console.WriteLine($"[STARTUP] Executando PoC.Populator");
        Console.WriteLine($"[STARTUP] Versão do Build: {versionInfo}");
        Console.WriteLine($"===================================================");

        var handler = Environment.GetEnvironmentVariable("_HANDLER");
        if (!string.IsNullOrEmpty(handler) && handler.Contains("PopulatorWorkerFunction"))
        {
            var wrapper = new PopulatorWorkerFunction();
            await LambdaBootstrapBuilder.Create<SQSEvent>(wrapper.FunctionHandler, new DefaultLambdaJsonSerializer())
                .Build()
                .RunAsync();
            return;
        }

        var builder = WebApplication.CreateBuilder();

        // Observability (Native OTel + ILogger)
        builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

        // Dependency Injection
        builder.Services.AddPopulatorInfrastructure(builder.Configuration);

        // Feature Flags (OpenFeature + Unleash)
        builder.Services.AddPoCFeatureFlags(options =>
        {
            options.UnleashApiUrl = builder.Configuration["FeatureFlags:UnleashApiUrl"] ?? "http://localhost:4242/api/";
            options.UnleashApiKey = builder.Configuration["FeatureFlags:UnleashApiKey"] ?? "default:development.unleash-insecure-api-token";
            options.UnleashAppName = "PoC-Populator";
            options.UnleashInstanceId = "populator";
            if (int.TryParse(builder.Configuration["FeatureFlags:FetchTogglesIntervalSeconds"], out var interval))
            {
                options.FetchTogglesIntervalSeconds = interval;
            }
        });

        // JSON Configuration
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
        });

        var app = builder.Build();

        app.MapGroup("/api/v1/populator")
           .MapPopulatorEndpoints();

        await app.RunAsync();
    }
}
