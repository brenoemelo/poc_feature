using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace PoC.Shared.Infrastructure.Extensions;

/// <summary>
/// Middleware that injects the current OpenTelemetry TraceId into the HTTP response headers.
/// </summary>
public sealed class TraceIdResponseMiddleware
{
    private const string TraceIdHeaderName = "X-Trace-Id";
    private readonly RequestDelegate _next;

    public TraceIdResponseMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var traceId = Activity.Current?.TraceId.ToString();
            if (!string.IsNullOrEmpty(traceId))
            {
                context.Response.Headers[TraceIdHeaderName] = traceId;
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
