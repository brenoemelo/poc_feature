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
builder.AddPoCObservability("PoC.Materials", "1.0.0");

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);
builder.Services.AddProblemDetails();

builder.Services.AddMaterialsInfrastructure(builder.Configuration);

// Feature Flags (OpenFeature + Unleash)
builder.Services.AddPoCFeatureFlags(builder.Configuration);

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<MaterialFormulationValidator>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.WriteIndented = true;
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

var app = builder.Build();

// Enable Observability Middleware (TraceId Injection, Flush)
app.UsePoCObservability();

// Middleware to fix double slashes from LocalStack/APIGW
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogDebug("[Middleware] Processing request: {Method} {Path}", context.Request.Method, context.Request.Path);
    
    if (context.Request.Path.Value?.Contains("//") == true)
    {
        context.Request.Path = context.Request.Path.Value.Replace("//", "/");
        logger.LogDebug("[Middleware] Fixed Path: {Path}", context.Request.Path);
    }
    
    try 
    {
        await next(context);
        logger.LogDebug("[Middleware] Request processed successfully: {StatusCode}", context.Response.StatusCode);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[Middleware] Request failed with exception");
        throw;
    }
});

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
