using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace PoC.Observability.Middleware;

/// <summary>
/// Middleware to force flush OpenTelemetry providers (Tracer and Meter) at the end of the request.
/// This is critical for AWS Lambda environments where the execution context freezes immediately after the response is sent.
/// </summary>
public class OpenTelemetryFlushMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OpenTelemetryFlushMiddleware> _logger;

    public OpenTelemetryFlushMiddleware(RequestDelegate next, ILogger<OpenTelemetryFlushMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        finally
        {
            // Only flush if we are in a Lambda environment or explicitly configured
            // But for now, we'll flush if the providers are available.
            // In a high-throughput server (Kestrel), this might add overhead, but for Lambda (low concurrency per instance) it's fine.
            // We can check environment variable to be safe.
            if (IsLambdaEnvironment())
            {
                FlushProviders(context.RequestServices);
            }
        }
    }

    private static bool IsLambdaEnvironment()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));
    }

    private void FlushProviders(IServiceProvider services)
    {
        try
        {
            var tracerProvider = services.GetService<TracerProvider>();
            tracerProvider?.ForceFlush();

            var meterProvider = services.GetService<MeterProvider>();
            meterProvider?.ForceFlush();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to flush OpenTelemetry providers.");
        }
    }
}
