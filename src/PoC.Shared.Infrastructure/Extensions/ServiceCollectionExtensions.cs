using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenFeature;
using OpenFeature.Providers.GOFeatureFlag;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
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
/// Extensions for <see cref="IServiceCollection"/> and <see cref="WebApplicationBuilder"/> to configure
/// observability (Serilog + OpenTelemetry) and feature flags (OpenFeature + GO Feature Flag).
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

            var otelOptions = context.Configuration.GetSection("Otel").Get<OtelOptions>() ?? new OtelOptions();
            if (!string.IsNullOrEmpty(otelOptions.Endpoint))
            {
                // Use the parameterless version to rely on standard environment variables
                // (OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_SERVICE_NAME, etc.)
                // This is more robust in Lambda/Docker as it handles protocol/path defaults.
                loggerConfiguration.WriteTo.OpenTelemetry();
            }

            // Imp-3: Reduce noise (Programmatic overrides if not in appsettings)
            loggerConfiguration
               .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
               .MinimumLevel.Override("Microsoft.Extensions.Http", Serilog.Events.LogEventLevel.Warning)
               .MinimumLevel.Override("System.Net.Http.HttpClient.OtlpTraceExporter", Serilog.Events.LogEventLevel.Error);
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
                .Enrich.WithOpenTelemetrySpanId()
                .WriteTo.Console(new CompactJsonFormatter());

            var otelOptions = config.GetSection("Otel").Get<OtelOptions>() ?? new OtelOptions();
            if (!string.IsNullOrEmpty(otelOptions.Endpoint))
            {
                loggerConfiguration.WriteTo.OpenTelemetry(options =>
                {
                    options.Endpoint = otelOptions.Endpoint;
                    options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
                    options.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = serviceName,
                        ["service.version"] = serviceVersion,
                        // Worker might not have HostingEnvironment easily accessible in this context, 
                        // but we can try to get it or just use the config/defaults.
                        // For now, let's omit env if not easily available or assume "Production" if not set.
                        // Actually, builder.Environment is available in the IHostApplicationBuilder, but here we are in the lambda.
                        // Let's check where `config` comes from. It is `services.GetRequiredService<IConfiguration>()`.
                        // We can get IHostEnvironment too.
                        ["deployment.environment"] = services.GetService<IHostEnvironment>()?.EnvironmentName ?? "Unknown"
                    };
                });
            }

            // Imp-3: Reduce noise
            loggerConfiguration
               .MinimumLevel.Override("Microsoft.Extensions.Http", Serilog.Events.LogEventLevel.Warning)
               .MinimumLevel.Override("System.Net.Http.HttpClient.OtlpTraceExporter", Serilog.Events.LogEventLevel.Error);
        });

        // 2. Common OTel
        ConfigureOpenTelemetry(builder.Services, builder.Configuration, builder.Environment.EnvironmentName, serviceName, serviceVersion, isWeb: false);

        return builder;
    }

    /// <summary>
    /// Configures Feature Flags using OpenFeature + GO Feature Flag provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPoCFeatureFlags(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("FeatureFlags").Get<FeatureFlagOptions>() ?? new FeatureFlagOptions();
        services.Configure<FeatureFlagOptions>(configuration.GetSection("FeatureFlags"));

        var providerOptions = new GOFeatureFlagProviderOptions
        {
            Endpoint = options.Endpoint,
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            // Ensure quick propagation of flag changes
            FlagChangePollingIntervalMs = TimeSpan.FromSeconds(1)
        };

        var provider = new GOFeatureFlagProvider(providerOptions);

        try
        {
#pragma warning disable VSTHRD002
            Api.Instance.SetProviderAsync(options.AppName, provider).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
            Log.Information("Feature Flags configured with GO Feature Flag at '{Endpoint}'.", options.Endpoint);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize Feature Flags provider at '{Endpoint}'. Application will start with default values.", options.Endpoint);
        }

        var client = Api.Instance.GetClient(options.AppName);
        services.AddSingleton(client);

        return services;
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

        // Parse Endpoint for filtering
        Uri? otlpEndpoint = null;
        if (!string.IsNullOrEmpty(options.Endpoint) && Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri))
        {
            otlpEndpoint = uri;
        }

        // Filter function to exclude OTLP exporter requests to prevent infinite loops
        Func<HttpRequestMessage, bool> httpFilter = req => 
        {
            if (req.RequestUri == null) return true;

            // 1. Check against configured endpoint if available
            if (otlpEndpoint != null && 
                string.Equals(req.RequestUri.Host, otlpEndpoint.Host, StringComparison.OrdinalIgnoreCase) &&
                req.RequestUri.Port == otlpEndpoint.Port)
            {
                return false;
            }

            // 2. Check for standard OTLP paths (safety net if endpoint is not configured correctly or differs slightly)
            // Common paths: /v1/traces, /v1/metrics, /v1/logs
            bool isOtlpPath = req.RequestUri.AbsolutePath.Contains("/v1/traces", StringComparison.OrdinalIgnoreCase) ||
                              req.RequestUri.AbsolutePath.Contains("/v1/metrics", StringComparison.OrdinalIgnoreCase) ||
                              req.RequestUri.AbsolutePath.Contains("/v1/logs", StringComparison.OrdinalIgnoreCase);

            if (isOtlpPath && (req.RequestUri.Port == 4317 || req.RequestUri.Port == 4318 || req.RequestUri.Port == 14318))
            {
                return false;
            }

            return true;
        };

        // Tracing
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(serviceName) // Enable custom tracing for this service
                    .AddHttpClientInstrumentation(o => o.FilterHttpRequestMessage = httpFilter)
                    .AddAWSInstrumentation();

                if (isWeb)
                {
                    tracing.AddAspNetCoreInstrumentation(o => o.RecordException = true);
                }

                if (otlpEndpoint == null)
                {
                    if (!string.IsNullOrEmpty(options.Endpoint))
                    {
                        // Log warning only once (e.g. here in tracing)
                        Log.Warning("Invalid OTLP Endpoint '{Endpoint}'. Falling back to Console exporter.", options.Endpoint);
                    }

                    tracing.AddConsoleExporter();
                }
                else
                {
                    tracing.AddOtlpExporter(o => ConfigureOtlpExporter(o, options, otlpEndpoint, isLambda, OtlpSignal.Traces));
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

                if (otlpEndpoint == null)
                {
                    metrics.AddConsoleExporter();
                }
                else
                {
                    metrics.AddOtlpExporter(o => ConfigureOtlpExporter(o, options, otlpEndpoint, isLambda, OtlpSignal.Metrics));
                }
            })
            .WithLogging(logging =>
            {
                logging
                    .SetResourceBuilder(resourceBuilder);

                if (otlpEndpoint == null)
                {
                    logging.AddConsoleExporter();
                }
                else
                {
                    logging.AddOtlpExporter(o => ConfigureOtlpExporter(o, options, otlpEndpoint, isLambda, OtlpSignal.Logs));
                }
            });
    }

    private enum OtlpSignal
    {
        Traces,
        Metrics,
        Logs
    }

    private static void ConfigureOtlpExporter(OtlpExporterOptions o, OtelOptions options, Uri endpoint, bool isLambda, OtlpSignal signal)
    {
        o.Protocol = ParseProtocol(options.Protocol);
        o.Headers = options.Headers;

        if (o.Protocol == OtlpExportProtocol.HttpProtobuf)
        {
            // Append proper path for HTTP if using a base URL
            var uriBuilder = new UriBuilder(endpoint);
            if (!uriBuilder.Path.EndsWith("/v1/traces") && !uriBuilder.Path.EndsWith("/v1/metrics") && !uriBuilder.Path.EndsWith("/v1/logs"))
            {
                var path = uriBuilder.Path.TrimEnd('/');
                string signalPath = signal switch
                {
                    OtlpSignal.Metrics => "/v1/metrics",
                    OtlpSignal.Logs => "/v1/logs",
                    _ => "/v1/traces"
                };
                uriBuilder.Path = $"{path}{signalPath}";
                o.Endpoint = uriBuilder.Uri;
            }
            else
            {
                 o.Endpoint = endpoint;
            }
        }
        else
        {
            o.Endpoint = endpoint;
        }

        if (isLambda)
        {
            o.ExportProcessorType = ExportProcessorType.Simple;
            o.TimeoutMilliseconds = 3000;
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
