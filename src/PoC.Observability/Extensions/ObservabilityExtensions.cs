using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Extensions.AWS.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PoC.Observability.Configuration;
using PoC.Observability.Diagnostics;

namespace PoC.Observability.Extensions;

public static class ObservabilityExtensions
{
    /// <summary>
    /// Flushes the OpenTelemetry providers (Tracer, Meter, and Logger).
    /// </summary>
    /// <param name="services">The service provider to retrieve providers from.</param>
    public static void FlushOpenTelemetryProviders(this IServiceProvider services)
    {
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("OpenTelemetryFlusher");
        try
        {
            var tracerProvider = services.GetService<TracerProvider>();
            var meterProvider = services.GetService<MeterProvider>();
            var loggerProvider = services.GetService<LoggerProvider>();

            var flushTasks = new List<Task>();
            if (tracerProvider != null) flushTasks.Add(Task.Run(() => tracerProvider.ForceFlush()));
            if (meterProvider != null) flushTasks.Add(Task.Run(() => meterProvider.ForceFlush()));
            if (loggerProvider != null) flushTasks.Add(Task.Run(() => loggerProvider.ForceFlush()));

            if (flushTasks.Any())
            {
                Task.WaitAll([.. flushTasks], TimeSpan.FromSeconds(3));
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to flush OpenTelemetry providers.");
        }
    }

    public static IServiceCollection AddStartUpMetrics(this IServiceCollection services)
    {
        services.AddSingleton<StartupTimer>();
        services.AddHostedService<InstrumentationSource>();
        return services;
    }

    /// <summary>
    /// Configures OpenTelemetry Observability (Logging, Tracing, Metrics) for Web Applications.
    /// </summary>
    public static WebApplicationBuilder AddPoCObservability(
        this WebApplicationBuilder builder,
        string serviceName,
        string serviceVersion)
    {
        return builder.AddPoCObservability(options =>
        {
            options.ServiceName = serviceName;
            options.ServiceVersion = serviceVersion;
        });
    }

    /// <summary>
    /// Configures OpenTelemetry Observability (Logging, Tracing, Metrics) for Web Applications with custom options.
    /// </summary>
    public static WebApplicationBuilder AddPoCObservability(
        this WebApplicationBuilder builder,
        Action<ObservabilityOptions> configureOptions)
    {
        var options = new ObservabilityOptions 
        { 
            ServiceName = "UnknownService",
            OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
            Environment = builder.Environment.EnvironmentName,
            ExportToConsole = builder.Environment.IsDevelopment()
        };
        configureOptions(options);

        ConfigureObservability(builder.Services, builder.Logging, options);
        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry Observability (Logging, Tracing, Metrics) for Worker/Lambda Hosts.
    /// </summary>
    public static HostApplicationBuilder AddPoCObservability(
        this HostApplicationBuilder builder,
        string serviceName,
        string serviceVersion)
    {
        return builder.AddPoCObservability(options =>
        {
            options.ServiceName = serviceName;
            options.ServiceVersion = serviceVersion;
        });
    }

    /// <summary>
    /// Configures OpenTelemetry Observability (Logging, Tracing, Metrics) for Worker/Lambda Hosts with custom options.
    /// </summary>
    public static HostApplicationBuilder AddPoCObservability(
        this HostApplicationBuilder builder,
        Action<ObservabilityOptions> configureOptions)
    {
        var options = new ObservabilityOptions 
        { 
            ServiceName = "UnknownService",
            OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
            Environment = builder.Environment.EnvironmentName,
            ExportToConsole = builder.Environment.IsDevelopment()
        };
        configureOptions(options);

        ConfigureObservability(builder.Services, builder.Logging, options);
        return builder;
    }

    private static void ConfigureObservability(
        IServiceCollection services,
        ILoggingBuilder loggingBuilder,
        ObservabilityOptions options)
    {
        // 0. Add Startup Metrics
        services.AddStartUpMetrics();

        // 1. Define Resource Builder
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = options.Environment,
                ["cloud.provider"] = "aws"
            })
            .AddAWSEC2Detector() // Requires OpenTelemetry.Resources.AWS
            .AddTelemetrySdk();

        // 2. Configure Logging
        loggingBuilder.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resourceBuilder);
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.ParseStateValues = true;

            if (!string.IsNullOrEmpty(options.OtlpEndpoint))
            {
                logging.AddOtlpExporter(otlp =>
                {
                    otlp.Endpoint = new Uri(options.OtlpEndpoint);
                    otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                });
            }

            if (options.ExportToConsole)
            {
                // We don't add ConsoleExporter to OTel logging because we will use the native JsonConsole below
                // logging.AddConsoleExporter(); 
            }
        });

        // Configure JsonConsole as the standard output format
        loggingBuilder.AddJsonConsole(json =>
        {
            json.IncludeScopes = true;
            json.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            json.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
        });

        // 3. Configure OpenTelemetry (Tracing & Metrics)
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .AddXRayTraceId() // Requires OpenTelemetry.Extensions.AWS
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(options.ServiceName)
                    .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                    .AddHttpClientInstrumentation()
                    .AddAWSInstrumentation(); // Requires OpenTelemetry.Instrumentation.AWS

                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(options.OtlpEndpoint);
                        otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
                }

                if (options.ExportToConsole)
                {
                    tracing.AddConsoleExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(options.ServiceName)
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("System.Net.Http")
                    .AddMeter("OpenTelemetry.Instrumentation.Http");

                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp =>
                    {
                        otlp.Endpoint = new Uri(options.OtlpEndpoint);
                        otlp.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
                }

                if (options.ExportToConsole)
                {
                    metrics.AddConsoleExporter();
                }
            });

        // 4. Configure Propagators (W3C + X-Ray + Baggage)
        Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
        {
            new TraceContextPropagator(), // W3C Standard
            new AWSXRayPropagator(),       // AWS X-Ray Specific
            new BaggagePropagator()       // Arbitrary Metadata
        }));
    }
}
