using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PoC.Observability.Configuration;
using PoC.Observability.Diagnostics;

namespace PoC.Observability.Extensions;

public static class ObservabilityExtensions
{
    private const string OtlpTimeoutEnvVar = "OTEL_EXPORTER_OTLP_TIMEOUT";

    public static IServiceCollection AddStartUpMetrics(this IServiceCollection services)
    {
        services.AddSingleton<StartupTimer>();
        services.AddSingleton<InstrumentationSource>();
        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry Observability (Tracing, Metrics, and Native Logging).
    /// </summary>
    public static IServiceCollection AddPoCObservability(
        this IServiceCollection services,
        Action<ObservabilityOptions> configureOptions)
    {
        var options = new ObservabilityOptions { ServiceName = "Unknown" };
        configureOptions(options);

        // 1. Build Resource
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion)
            .AddTelemetrySdk()
            .AddEnvironmentVariableDetector();

        // 2. Configure OpenTelemetry (Tracing & Metrics)
        var otel = services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(options.ServiceName)
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation(o => o.RecordException = true) // Capture exceptions
                    .AddAWSInstrumentation() // AWS SDK
                    .AddAWSLambdaConfigurations(o => o.DisableAwsXRayContextExtraction = true); // Lambda

                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(o => ConfigureOtlp(o, options.OtlpEndpoint));
                }
                
                if (options.EnableConsoleLogging)
                {
                    // tracing.AddConsoleExporter(); // Optional: Traces in console are verbose
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddRuntimeInstrumentation() // GC, Memory
                    .AddMeter(options.ServiceName);

                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(o => ConfigureOtlp(o, options.OtlpEndpoint));
                }
            });

        // 3. Configure Native Logging (ILogger -> OTLP)
        services.Configure<OpenTelemetryLoggerOptions>(opt =>
        {
            opt.SetResourceBuilder(resourceBuilder);
            opt.IncludeScopes = true;
            opt.ParseStateValues = true;
            opt.IncludeFormattedMessage = true;
        });

        return services;
    }

    /// <summary>
    /// Configures the logging builder to export to OpenTelemetry.
    /// </summary>
    public static ILoggingBuilder AddPoCOTelLogging(
        this ILoggingBuilder builder,
        Action<ObservabilityOptions> configureOptions)
    {
        var options = new ObservabilityOptions { ServiceName = "Unknown", EnableConsoleLogging = true };
        configureOptions(options);

        builder.ClearProviders(); // Start clean (removes default Console/Debug)
        
        if (options.EnableConsoleLogging)
        {
            builder.AddConsole();
        }

        builder.AddOpenTelemetry(logging =>
        {
            if (!string.IsNullOrEmpty(options.OtlpEndpoint))
            {
                logging.AddOtlpExporter(o => ConfigureOtlp(o, options.OtlpEndpoint));
            }
        });

        return builder;
    }

    private static void ConfigureOtlp(OtlpExporterOptions o, string endpoint)
    {
        o.Endpoint = new Uri(endpoint);
        o.Protocol = OtlpExportProtocol.Grpc; // Defaulting to gRPC for performance

        // Handle HTTP/Protobuf if endpoint implies it (simple heuristic)
        if (endpoint.Contains("http") && (endpoint.EndsWith("/v1/logs") || endpoint.EndsWith("/v1/traces") || endpoint.EndsWith("/v1/metrics")))
        {
             o.Protocol = OtlpExportProtocol.HttpProtobuf;
        }
        else if (endpoint.Contains(":4318")) // Standard HTTP port
        {
             o.Protocol = OtlpExportProtocol.HttpProtobuf;
        }

        // Optimize for Lambda: Short timeout to flush before freeze
        // But respect env var if set
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(OtlpTimeoutEnvVar)))
        {
            o.TimeoutMilliseconds = 1000; 
        }
    }
}
