using Microsoft.AspNetCore.Builder;
using PoC.Observability.Middleware;

namespace PoC.Observability.Extensions;

/// <summary>
/// Extensions for <see cref="WebApplication"/> to configure observability middleware.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures OpenTelemetry Observability Middleware (TraceId Injection, Flush).
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application for chaining.</returns>
    public static WebApplication UsePoCObservability(this WebApplication app)
    {
        app.UseMiddleware<TraceIdResponseMiddleware>();
        app.UseMiddleware<OpenTelemetryFlushMiddleware>();

        return app;
    }
}
