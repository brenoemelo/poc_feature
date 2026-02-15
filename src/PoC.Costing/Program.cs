using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using FluentValidation;
using PoC.Costing.API.Endpoints;
using PoC.Costing.Infrastructure;
using PoC.Shared.Validators;
using Serilog;
using Serilog.Formatting.Compact;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(new CompactJsonFormatter());
});

// AWS Lambda Hosting
Console.WriteLine("STARTING UP PoC.Costing with REST API");
builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

// Dependency Injection
builder.Services.AddCostingInfrastructure(builder.Configuration);

// Validators
builder.Services.AddValidatorsFromAssemblyContaining<ComponentPriceRequestValidator>();

// JSON Configuration
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error" });
    });
});

app.MapGroup("/costing")
   .MapCostingEndpoints();

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
