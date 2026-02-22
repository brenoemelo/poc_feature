# 02 - Extraction Plan: PoC.Observability (Native AOT Ready)

## Strategy
We are abandoning Serilog in favor of the built-in `Microsoft.Extensions.Logging`. OpenTelemetry will hook directly into the `ILogger` provider to export logs via OTLP. This reduces memory footprint and ensures compatibility with Native AOT.

## New Project Structure
Create a new Class Library: `src/PoC.Observability/PoC.Observability.csproj`.

### Dependencies (NuGet)
Use **only** official OpenTelemetry packages:
*   `OpenTelemetry.Extensions.Hosting`
*   `OpenTelemetry.Instrumentation.AspNetCore`
*   `OpenTelemetry.Instrumentation.Http`
*   `OpenTelemetry.Instrumentation.AWS`
*   `OpenTelemetry.Instrumentation.AWSLambda`
*   `OpenTelemetry.Exporter.OpenTelemetryProtocol`
*   `OpenTelemetry.Instrumentation.Runtime` (New: GC/Memory metrics)

**Removed**: `Serilog.*`

### Code Configuration

1.  **Logging Configuration**:
    Instead of `UseSerilog`, we configure the logging builder in `IServiceCollection`.

    ```csharp
    // PoC.Observability.Extensions.ObservabilityExtensions.cs

    public static IServiceCollection AddPoCObservability(
        this IServiceCollection services, 
        ObservabilityOptions options)
    {
        // 1. Configure OpenTelemetry (Traces & Metrics)
        services.AddOpenTelemetry()
            .WithTracing(tracing => { ... })
            .WithMetrics(metrics => { ... });

        // 2. Configure OpenTelemetry Logging
        // This hooks into ILogger and exports to OTLP
        services.Configure<OpenTelemetryLoggerOptions>(opt =>
        {
            opt.IncludeScopes = true;
            opt.ParseStateValues = true;
            opt.IncludeFormattedMessage = true;
        });

        return services;
    }

    public static ILoggingBuilder AddPoCOTelLogging(this ILoggingBuilder builder, ObservabilityOptions options)
    {
        builder.ClearProviders(); // Option: remove Console/Debug if strictly OTLP
        builder.AddOpenTelemetry(logging =>
        {
            logging.AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri(options.OtlpEndpoint);
                otlp.Protocol = OtlpExportProtocol.Grpc; // or HttpProtobuf
            });
        });
        
        return builder;
    }
    ```

### Usage in `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Telemetry
builder.Services.AddPoCObservability(o => ...);

// 2. Logging
builder.Logging.AddPoCOTelLogging(o => ...);
```

## Benefits for Lambda
*   **Startup Time**: Faster than initializing Serilog's static implementation.
*   **Memory**: No duplicate log buffers (Serilog buffer + OTel buffer).
*   **Trace Correlation**: Native `Activity.Current` is automatically injected into `ILogger` scopes by the OTel provider.

## Migration (Code Changes)
*   **Find**: `Log.Information(...)` (Serilog static)
*   **Replace**: `_logger.LogInformation(...)` (DI injected `ILogger<T>`).
*   **Remove**: `UseSerilogRequestLogging()` middleware.
*   **Add**: `builder.Services.Configure<AspNetCoreInstrumentationOptions>(o => o.RecordException = true);` to capture exceptions in traces instead.
