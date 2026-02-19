using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using PoC.Populator.API.Endpoints;
using PoC.Populator.Infrastructure;
using PoC.Shared.Infrastructure.Extensions;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

var builder = WebApplication.CreateBuilder(args);

// Observability (Serilog + OpenTelemetry)
builder.AddPoCObservability("PoC-Populator", "1.0.0");

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// Dependency Injection
builder.Services.AddPopulatorInfrastructure(builder.Configuration);

// Feature Flags (OpenFeature + Unleash)
builder.Services.AddPoCFeatureFlags(builder.Configuration);

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
