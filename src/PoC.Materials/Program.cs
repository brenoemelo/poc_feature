using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using FluentValidation;
using PoC.Materials.API.Endpoints;
using PoC.Materials.Infrastructure;
using PoC.Shared.Infrastructure.Extensions;
using PoC.Shared.Validators;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

var builder = WebApplication.CreateBuilder(args);

// Observability (Serilog + OpenTelemetry)
builder.AddPoCObservability("PoC-Materials", "1.0.0");

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

builder.Services.AddMaterialsInfrastructure(builder.Configuration);

// Feature Flags (OpenFeature + Unleash)
builder.Services.AddPoCFeatureFlags(builder.Configuration);

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
