using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PoC.Shared.Infrastructure.Configuration;
using PoC.Shared.Infrastructure.Diagnostics;
using Serilog;
using Serilog.Enrichers.OpenTelemetry;
using Serilog.Formatting.Compact;

namespace PoC.Shared.Infrastructure.Extensions;

/// <summary>
/// Extensions for <see cref="IServiceCollection"/> and <see cref="WebApplicationBuilder"/> to configure observability.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures structured logging (Serilog) and OpenTelemetry (Tracing/Metrics) for Web Applications.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="serviceVersion">The version of the service.</param>
    /// <returns>The builder for chaining.</returns>
    public static WebApplicationBuilder AddPoCObservability(
        this WebApplicationBuilder builder,
        string serviceName,
        string serviceVersion)
    {
        // 1. Serilog (Host-level for Web)
        builder.Host.UseSerilog((context, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithOpenTelemetryTraceId()
                .Enrich.WithOpenTelemetrySpanId()
                .WriteTo.Console(new CompactJsonFormatter());

            // Imp-3: Reduce noise (Programmatic overrides if not in appsettings)
            loggerConfiguration
               .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
               .MinimumLevel.Override("Microsoft.Extensions.Http", Serilog.Events.LogEventLevel.Warning);
        });

        // 2. Common OTel
        ConfigureOpenTelemetry(builder.Services, builder.Configuration, builder.Environment.EnvironmentName, serviceName, serviceVersion, isWeb: true);

        return builder;
    }

    /// <summary>
    /// Configures structured logging (Serilog) and OpenTelemetry (Tracing/Metrics) for Workers/Hosts.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="serviceVersion">The version of the service.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddPoCObservability(
        this IHostApplicationBuilder builder,
        string serviceName,
        string serviceVersion)
    {
        // 1. Serilog (Services-level for Worker)
        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            var config = services.GetRequiredService<IConfiguration>();
            loggerConfiguration
                .ReadFrom.Configuration(config)
                .Enrich.FromLogContext()
                .Enrich.WithOpenTelemetryTraceId()
                .Enrich.WithOpenTelemetrySpanId()
                .WriteTo.Console(new CompactJsonFormatter());

            // Imp-3: Reduce noise
            loggerConfiguration
               .MinimumLevel.Override("Microsoft.Extensions.Http", Serilog.Events.LogEventLevel.Warning);
        });

        // 2. Common OTel
        ConfigureOpenTelemetry(builder.Services, builder.Configuration, builder.Environment.EnvironmentName, serviceName, serviceVersion, isWeb: false);

        return builder;
    }

    private static void ConfigureOpenTelemetry(
        IServiceCollection services, 
        IConfiguration configuration, 
        string environment, 
        string serviceName, 
        string serviceVersion,
        bool isWeb)
    {
        services.Configure<OtelOptions>(configuration.GetSection("Otel"));
        var options = configuration.GetSection("Otel").Get<OtelOptions>() ?? new OtelOptions();
        var envName = options.Environment ?? environment;
        
        // Detect if running in AWS Lambda
        var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

        // Startup Timer
        var startupTimer = new StartupTimer();
        services.AddSingleton(startupTimer);
        
        // Custom Startup Metric - Registered once to avoid duplicates (C2 fix)
        services.AddSingleton<InstrumentationSource>(sp => new InstrumentationSource(startupTimer));

        // Resource
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion,
                serviceInstanceId: Guid.NewGuid().ToString())
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = envName
            })
            .AddEnvironmentVariableDetector();

        // Tracing
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddAWSInstrumentation();

                if (isWeb)
                {
                    tracing.AddAspNetCoreInstrumentation(o => o.RecordException = true);
                }

                if (string.IsNullOrEmpty(options.Endpoint))
                {
                    tracing.AddConsoleExporter();
                }
                else
                {
                    if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpointUri))
                    {
                        Log.Warning("Invalid OTLP Endpoint '{Endpoint}'. Falling back to Console exporter.", options.Endpoint);
                        tracing.AddConsoleExporter();
                    }
                    else
                    {
                        tracing.AddOtlpExporter(o => ConfigureOtlpExporter(o, options, endpointUri, isLambda));
                    }
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddRuntimeInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("app.startup");

                if (isWeb)
                {
                    metrics.AddAspNetCoreInstrumentation();
                }

                if (string.IsNullOrEmpty(options.Endpoint))
                {
                    metrics.AddConsoleExporter();
                }
                else
                {
                    if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpointUri))
                    {
                        // No need to log warning again, tracing block already did it
                        metrics.AddConsoleExporter();
                    }
                    else
                    {
                        metrics.AddOtlpExporter(o => ConfigureOtlpExporter(o, options, endpointUri, isLambda));
                    }
                }
            });
    }

    private static void ConfigureOtlpExporter(OtlpExporterOptions o, OtelOptions options, Uri endpoint, bool isLambda)
    {
        o.Endpoint = endpoint;
        o.Protocol = ParseProtocol(options.Protocol);
        o.Headers = options.Headers;

        if (isLambda)
        {
            o.ExportProcessorType = ExportProcessorType.Simple;
        }
    }

    private static OtlpExportProtocol ParseProtocol(string protocol)
    {
        switch (protocol.ToLowerInvariant())
        {
            case "grpc": return OtlpExportProtocol.Grpc;
            case "http": return OtlpExportProtocol.HttpProtobuf;
            default:
                Log.Warning("Unknown OTLP protocol '{Protocol}'. Defaulting to gRPC.", protocol);
                return OtlpExportProtocol.Grpc;
        }
    }
}
