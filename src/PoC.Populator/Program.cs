using PoC.Populator.Endpoints;
using PoC.Populator.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// AWS Lambda Hosting
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// Dependency Injection
builder.Services.AddPopulatorInfrastructure(builder.Configuration);

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

app.MapGroup("/populate")
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
