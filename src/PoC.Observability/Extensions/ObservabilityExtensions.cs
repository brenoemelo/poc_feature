using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Exporter;
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
    private static int _propagatorsInitialized;
    /// <summary>
    /// Flushes the OpenTelemetry providers (Tracer, Meter, and Logger).
    /// </summary>
    /// <param name="services">The service provider to retrieve providers from.</param>
    public static void FlushOpenTelemetryProviders(this IServiceProvider services)
    {
        var logger = services.GetService<ILoggerFactory>()?.CreateLogger("OpenTelemetryFlusher");
        try
        {
            const int timeoutMs = 3000;
            services.GetService<TracerProvider>()?.ForceFlush(timeoutMs);
            services.GetService<MeterProvider>()?.ForceFlush(timeoutMs);
            services.GetService<LoggerProvider>()?.ForceFlush(timeoutMs);
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
            Enabled = bool.TryParse(builder.Configuration["Observability:Enabled"], out var e1) ? e1 : false,
            ServiceName = "UnknownService",
            OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
            OtlpProtocol = builder.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"],
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
            Enabled = bool.TryParse(builder.Configuration["Observability:Enabled"], out var e2) ? e2 : false,
            ServiceName = "UnknownService",
            OtlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"],
            OtlpProtocol = builder.Configuration["OTEL_EXPORTER_OTLP_PROTOCOL"],
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
        // 0. Configure Base Logging (always available)
        loggingBuilder.AddJsonConsole(json =>
        {
            json.IncludeScopes = true;
            json.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            json.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
        });

        if (!options.Enabled)
        {
            return;
        }

        options.Validate();

        // 1. Add Startup Metrics
        services.AddStartUpMetrics();

        // 2. Define Resource Builder
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
                logging.AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options));
            }
        });

        // JSON console was already added initially

        // 3. Configure OpenTelemetry (Tracing & Metrics)
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .AddXRayTraceId()
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(options.ServiceName)
                    .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                    .AddHttpClientInstrumentation()
                    .AddAWSInstrumentation();

                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options));
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
                    .AddMeter("PoC.FeatureFlags") // Legacy/Custom Meter
                    .AddMeter("OpenFeature*")     // Standard OpenFeature Meter
                    .AddMeter("app.startup")
                    .AddMeter("System.Net.Http")
                    .AddMeter("OpenTelemetry.Instrumentation.Http")
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrEmpty(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options));
                }

                if (options.ExportToConsole)
                {
                    metrics.AddConsoleExporter();
                }
            });

        // 4. Configure Propagators (W3C + X-Ray + Baggage) — once per process
        if (Interlocked.Exchange(ref _propagatorsInitialized, 1) == 0)
        {
            Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(
            [
                new TraceContextPropagator(),
                new AWSXRayPropagator(),
                new BaggagePropagator()
            ]));
        }
    }

    private static void ConfigureOtlpExporter(OtlpExporterOptions otlp, ObservabilityOptions options)
    {
        otlp.Endpoint = new Uri(options.OtlpEndpoint!);
        otlp.Protocol = string.Equals(options.OtlpProtocol, "http/protobuf", StringComparison.OrdinalIgnoreCase)
            ? OtlpExportProtocol.HttpProtobuf
            : OtlpExportProtocol.Grpc;
    }
}
