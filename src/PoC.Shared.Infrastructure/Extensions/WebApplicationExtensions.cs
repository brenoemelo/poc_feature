using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
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

                Log.Error(exception, "Unhandled exception occurred: {Message}", exception?.Message);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var problem = new
                {
                    type = "about:blank",
                    title = "Internal Server Error",
                    status = StatusCodes.Status500InternalServerError,
                    detail = app.Environment.IsDevelopment() ? exception?.ToString() : "An unexpected error occurred",
                    instance = context.Request.Path
                };

                await context.Response.WriteAsJsonAsync(problem);
            });
        });

        return app;
    }
}
