using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using FluentValidation;
using PoC.Costing.API.Endpoints;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Domain.Services;
using PoC.Costing.Infrastructure;
using PoC.Costing.Infrastructure.ExternalServices;
using PoC.Shared.Infrastructure.Extensions;
using PoC.Shared.Validators;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

var builder = WebApplication.CreateBuilder(args);

// Observability (Serilog + OpenTelemetry)
builder.AddPoCObservability("PoC-Costing", "1.0.0");

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// Dependency Injection
builder.Services.AddCostingInfrastructure(builder.Configuration);

// Feature Flags (OpenFeature + GO Feature Flag)
builder.Services.AddPoCFeatureFlags(builder.Configuration);

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
