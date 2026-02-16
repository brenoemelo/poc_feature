using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace PoC.Shared.Infrastructure.Extensions;

/// <summary>
/// Extensions for <see cref="WebApplication"/> to configure shared defaults.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures shared middleware defaults (Exception handling, Request logging).
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application for chaining.</returns>
    public static WebApplication UsePoCDefaults(this WebApplication app)
    {
        app.UseMiddleware<TraceIdResponseMiddleware>();
        app.UseSerilogRequestLogging();

        app.UseExceptionHandler(exceptionHandlerApp =>
        {
            exceptionHandlerApp.Run(async context =>
            {
                var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                var exception = exceptionHandlerPathFeature?.Error;

                var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("PoC.Shared.Infrastructure.Extensions.WebApplicationExtensions");
                logger.LogError(exception, "Unhandled exception occurred: {Message}", exception?.Message);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var problem = new
                {
                    type = "about:blank",
                    title = "Internal Server Error",
                    status = StatusCodes.Status500InternalServerError,
                    detail = app.Environment.IsDevelopment() ? exception?.ToString() : "An unexpected error occurred",
                    instance = context.Request.Path,
                    traceId = Activity.Current?.TraceId.ToString()
                };

                await context.Response.WriteAsJsonAsync(problem);
            });
        });

        return app;
    }
}
