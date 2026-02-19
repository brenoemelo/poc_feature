using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

namespace PoC.Shared.Infrastructure.Extensions;

/// <summary>
/// Extensions for <see cref="IServiceCollection"/> to configure shared infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Configures OpenTelemetry Observability (Logging, Tracing, Metrics).
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="serviceVersion">The version of the service.</param>
    /// <returns>The updated builder.</returns>
    public static WebApplicationBuilder AddPoCObservability(
        this WebApplicationBuilder builder,
        string serviceName,
        string serviceVersion)
    {
        // 1. Configure Serilog (Logging)
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithProperty("ServiceVersion", serviceVersion)
                .WriteTo.Console();

            // OTLP Export for Logs (if enabled)
            // configuration.WriteTo.OpenTelemetry(options =>
            // {
            //     options.Endpoint = "http://localhost:4317";
            //     options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
            //     options.ResourceAttributes = new Dictionary<string, object>
            //     {
            //         ["service.name"] = serviceName,
            //         ["service.version"] = serviceVersion
            //     };
            // });
        });

        // 2. Configure OpenTelemetry (Tracing & Metrics)
        var otelBuilder = builder.Services.AddOpenTelemetry();

        // Configure Tracing
        otelBuilder.WithTracing(tracerProviderBuilder =>
        {
            tracerProviderBuilder
                .AddSource(serviceName)
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation();
                // .AddAWSInstrumentation(); // Helper to trace AWS SDK calls
                // .AddProcessInstrumentation() // Requires OpenTelemetry.Instrumentation.Process; // Requires OpenTelemetry.Instrumentation.Process

            // Add Lambda Instrumentation if running in Lambda
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")))
            {
                tracerProviderBuilder.AddAWSLambdaConfigurations(options =>
                {
                    options.DisableAwsXRayContextExtraction = true; // Use OTel propagation
                });
            }

            // Export to OTLP (Collector)
            tracerProviderBuilder.AddOtlpExporter();
            // .AddConsoleExporter(); // Debugging only
        });

        // Configure Metrics
        otelBuilder.WithMetrics(meterProviderBuilder =>
        {
            meterProviderBuilder
                .AddMeter(serviceName)
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
                .AddHttpClientInstrumentation()
                .AddAspNetCoreInstrumentation();
                // .AddProcessInstrumentation() // Requires OpenTelemetry.Instrumentation.Process
                // .AddRuntimeInstrumentation()

            // Export to OTLP
            meterProviderBuilder.AddOtlpExporter();
        });

        return builder;
    }

    // /// <summary>
    // /// Configures structured logging (Serilog) and OpenTelemetry (Tracing/Metrics) for Workers/Hosts.
    // /// </summary>
    // /// <param name="builder">The host application builder.</param>
    // /// <param name="serviceName">The name of the service.</param>
    // /// <param name="serviceVersion">The version of the service.</param>
    // /// <returns>The builder for chaining.</returns>
    // public static IHostApplicationBuilder AddPoCObservability(
    //     this IHostApplicationBuilder builder,
    //     string serviceName,
    //     string serviceVersion)
    // {
    //     // 1. Serilog (Services-level for Worker)
    //     builder.Services.AddSerilog((services, loggerConfiguration) =>
    //     {
    //         var config = services.GetRequiredService<IConfiguration>();
    //         loggerConfiguration
    //             .ReadFrom.Configuration(config)
    //             .Enrich.FromLogContext()
    //             .Enrich.WithOpenTelemetryTraceId()
    //             .Enrich.WithOpenTelemetrySpanId()
    //             .Enrich.WithOpenTelemetrySpanId()
    //             .WriteTo.Console(new CompactJsonFormatter());

    //         var otelOptions = config.GetSection("Otel").Get<OtelOptions>() ?? new OtelOptions();
    //         if (!string.IsNullOrEmpty(otelOptions.Endpoint))
    //         {
    //             loggerConfiguration.WriteTo.OpenTelemetry(options => ConfigureSerilogOtlp(options, otelOptions, serviceName, serviceVersion, services.GetService<IHostEnvironment>()?.EnvironmentName ?? "Unknown"));
    //         }

    //         // Imp-3: Reduce noise
    //         loggerConfiguration
    //            .MinimumLevel.Override("Microsoft.Extensions.Http", Serilog.Events.LogEventLevel.Warning)
    //            .MinimumLevel.Override("System.Net.Http.HttpClient.OtlpTraceExporter", Serilog.Events.LogEventLevel.Error);
    //     });

    //     // 2. Common OTel
    //     ConfigureOpenTelemetry(builder.Services, builder.Configuration, builder.Environment.EnvironmentName, serviceName, serviceVersion, isWeb: false);

    //     return builder;
    // }

    /// <summary>
    /// Configures Feature Flags using OpenFeature + Unleash provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    // public static IServiceCollection AddPoCFeatureFlags(
    //     this IServiceCollection services,
    //     IConfiguration configuration)
    // {
    //     var options = configuration.GetSection("FeatureFlags").Get<FeatureFlagOptions>() ?? new FeatureFlagOptions();
    //     services.Configure<FeatureFlagOptions>(configuration.GetSection("FeatureFlags"));

    //     var settings = new UnleashSettings
    //     {
    //         AppName = options.UnleashAppName,
    //         InstanceTag = options.UnleashInstanceId,
    //         UnleashApi = new Uri(options.UnleashApiUrl),
    //         CustomHttpHeaders = new Dictionary<string, string>
    //         {
    //             { "Authorization", options.UnleashApiKey ?? string.Empty }
    //         }
    //     };

    //     var unleashClient = new DefaultUnleash(settings);
    //     var provider = new UnleashProvider(unleashClient);
    //     var providerName = "Unleash";

    //     try
    //     {
    // #pragma warning disable VSTHRD002
    //         Api.Instance.SetProviderAsync(options.AppName, provider).GetAwaiter().GetResult();
    // #pragma warning restore VSTHRD002
    //         Log.Information("Feature Flags configured with provider '{Provider}'.", providerName);
    //     }
    //     catch (Exception ex)
    //     {
    //         Log.Error(ex, "Failed to initialize Feature Flags provider '{Provider}'. Application will start with default values.", providerName);
    //     }

    //     var client = Api.Instance.GetClient(options.AppName);
    //     services.AddSingleton(client);

    //     return services;
    // }

    // private static void ConfigureOpenTelemetry(
    //     IServiceCollection services, 
    //     IConfiguration configuration, 
    //     string environment, 
    //     string serviceName, 
    //     string serviceVersion,
    //     bool isWeb)
    // {
    //     services.Configure<OtelOptions>(configuration.GetSection("Otel"));
    //     var options = configuration.GetSection("Otel").Get<OtelOptions>() ?? new OtelOptions();
    //     var envName = options.Environment ?? environment;
        
    //     // Detect if running in AWS Lambda
    //     var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

    //     // Startup Timer
    //     var startupTimer = new StartupTimer();
    //     services.AddSingleton(startupTimer);
        
    //     // Custom Startup Metric - Registered once to avoid duplicates (C2 fix)
    //     services.AddSingleton<InstrumentationSource>(sp => new InstrumentationSource(startupTimer));

    //     // Resource
    //     var resourceBuilder = ResourceBuilder.CreateDefault()
    //         .AddService(
    //             serviceName: serviceName,
    //             serviceVersion: serviceVersion,
    //             serviceInstanceId: Guid.NewGuid().ToString())
    //         .AddAttributes(new Dictionary<string, object>
    //         {
    //             ["deployment.environment"] = envName
    //         })
    //         .AddEnvironmentVariableDetector();

    //     // Parse Endpoint for filtering
    //     Uri? otlpEndpoint = null;
    //     if (!string.IsNullOrEmpty(options.Endpoint) && Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var uri))
    //     {
    //         otlpEndpoint = uri;
    //     }

    //     // Filter function to exclude OTLP exporter requests to prevent infinite loops
    //     Func<HttpRequestMessage, bool> httpFilter = req => 
    //     {
    //         if (req.RequestUri == null) return true;

    //         // 1. Check against configured endpoint if available
    //         if (otlpEndpoint != null && 
    //             string.Equals(req.RequestUri.Host, otlpEndpoint.Host, StringComparison.OrdinalIgnoreCase) &&
    //             req.RequestUri.Port == otlpEndpoint.Port)
    //         {
    //             return false;
    //         }

    //         // 2. Check for standard OTLP paths (safety net if endpoint is not configured correctly or differs slightly)
    //         // Common paths: /v1/traces, /v1/metrics, /v1/logs
    //         bool isOtlpPath = req.RequestUri.AbsolutePath.Contains("/v1/traces", StringComparison.OrdinalIgnoreCase) ||
    //                           req.RequestUri.AbsolutePath.Contains("/v1/metrics", StringComparison.OrdinalIgnoreCase) ||
    //                           req.RequestUri.AbsolutePath.Contains("/v1/logs", StringComparison.OrdinalIgnoreCase);

    //         if (isOtlpPath && (req.RequestUri.Port == 4317 || req.RequestUri.Port == 4318 || req.RequestUri.Port == 14318))
    //         {
    //             return false;
    //         }

    //         return true;
    //     };

    //     // Tracing
    //     services.AddOpenTelemetry()
    //         .WithTracing(tracing =>
    //         {
    //             tracing
    //                 .SetResourceBuilder(resourceBuilder)
    //                 .AddSource(serviceName) // Enable custom tracing for this service
    //                 .AddHttpClientInstrumentation(o => o.FilterHttpRequestMessage = httpFilter);
    //                 // .AddAWSInstrumentation()
    //                 // .AddAWSLambdaConfigurations(options =>
    //                 // {
    //                 //    options.DisableAwsXRayContextExtraction = true;
    //                 // });

    //             if (isWeb)
    //             {
    //                 tracing.AddAspNetCoreInstrumentation(o => o.RecordException = true); // Capture exceptions
    //             }

    //             if (otlpEndpoint == null)
    //             {
    //                 if (!string.IsNullOrEmpty(options.Endpoint))
    //                 {
    //                     // Log warning only once (e.g. here in tracing)
    //                     Log.Warning("Invalid OTLP Endpoint '{Endpoint}'. Falling back to Console exporter.", options.Endpoint);
    //                 }

    //                 tracing.AddConsoleExporter();
    //             }
    //             else
    //             {
    //                 tracing.AddOtlpExporter(o => ConfigureOtlpExporter(o, options, otlpEndpoint, isLambda, OtlpSignal.Traces));
    //             }
    //         })
    //         .WithMetrics(metrics =>
    //         {
    //             metrics
    //                 .SetResourceBuilder(resourceBuilder)
    //                 // .AddRuntimeInstrumentation()
    //                 // .AddProcessInstrumentation()
    //                 .AddHttpClientInstrumentation()
    //                 .AddMeter("app.startup")
    //                 .AddView(
    //                     "http.server.request.duration",
    //                     new ExplicitBucketHistogramConfiguration
    //                     {
    //                         Boundaries = new double[] { 0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }
    //                     })
    //                 .AddView(
    //                     "http.client.request.duration",
    //                     new ExplicitBucketHistogramConfiguration
    //                     {
    //                         Boundaries = new double[] { 0, 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 }
    //                     });

    //             if (isWeb)
    //             {
    //                 metrics.AddAspNetCoreInstrumentation();
    //             }

    //             if (otlpEndpoint == null)
    //             {
    //                 metrics.AddConsoleExporter();
    //             }
    //             else
    //             {
    //                 metrics.AddOtlpExporter((o, m) => 
    //                 {
    //                     ConfigureOtlpExporter(o, options, otlpEndpoint, isLambda, OtlpSignal.Metrics);
    //                     if (isLambda)
    //                     {
    //                         // In Lambda, background threads may not run reliably. 
    //                         // We set a very short interval to increase the chance of export before freeze.
    //                         // Ideally, we should use AWS Lambda instrumentation wrapper or ForceFlush.
    //                         m.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 100;
    //                         m.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = 1000;
    //                     }
    //                 });
    //             }
    //         })
    //         .WithLogging(logging =>
    //         {
    //             logging
    //                 .SetResourceBuilder(resourceBuilder);

    //             if (otlpEndpoint == null)
    //             {
    //                 logging.AddConsoleExporter();
    //             }
    //             else
    //             {
    //                 logging.AddOtlpExporter(o => ConfigureOtlpExporter(o, options, otlpEndpoint, isLambda, OtlpSignal.Logs));
    //             }
    //         });
    // }

    // private static void ConfigureSerilogOtlp(
    //     Serilog.Sinks.OpenTelemetry.BatchedOpenTelemetrySinkOptions options, 
    //     OtelOptions otelOptions,
    //     string serviceName,
    //     string serviceVersion,
    //     string environment)
    // {
    //     // 1. Protocol
    //     options.Protocol = otelOptions.Protocol?.ToLowerInvariant() == "http" 
    //         ? Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf 
    //         : Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;

    //     // 2. Endpoint
    //     if (options.Protocol == Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf && !string.IsNullOrEmpty(otelOptions.Endpoint))
    //     {
    //         var uriBuilder = new UriBuilder(otelOptions.Endpoint);
    //         // Serilog requires full path for HTTP (e.g., http://host:4318/v1/logs)
    //         if (!uriBuilder.Path.EndsWith("/v1/logs"))
    //         {
    //             var path = uriBuilder.Path.TrimEnd('/');
    //             uriBuilder.Path = $"{path}/v1/logs";
    //             options.Endpoint = uriBuilder.Uri.ToString();
    //         }
    //         else
    //         {
    //             options.Endpoint = otelOptions.Endpoint;
    //         }
    //     }
    //     else
    //     {
    //         options.Endpoint = otelOptions.Endpoint;
    //     }

    //     // 3. Headers
    //     if (!string.IsNullOrEmpty(otelOptions.Headers))
    //     {
    //         var headers = new Dictionary<string, string>();
    //         foreach (var header in otelOptions.Headers.Split(',', StringSplitOptions.RemoveEmptyEntries))
    //         {
    //             var parts = header.Split('=', 2);
    //             if (parts.Length == 2)
    //             {
    //                 headers[parts[0].Trim()] = parts[1].Trim();
    //             }
    //         }

    //         options.Headers = headers;
    //     }

    //     // 4. Resources
    //     var envName = otelOptions.Environment ?? environment;
    //     Console.WriteLine($"[ConfigureSerilogOtlp] Setting ResourceAttributes for {serviceName}");

    //     var resources = new Dictionary<string, object>
    //     {
    //         ["service.name"] = serviceName,
    //         ["service_name"] = serviceName,
    //         ["service.version"] = serviceVersion,
    //         ["deployment.environment"] = envName
    //     };

    //     if (options.ResourceAttributes != null)
    //     {
    //         foreach (var kv in options.ResourceAttributes)
    //         {
    //             if (!resources.ContainsKey(kv.Key))
    //             {
    //                 resources[kv.Key] = kv.Value;
    //             }
    //         }
    //     }

    //     options.ResourceAttributes = resources;
    // }

    // private enum OtlpSignal
    // {
    //     Traces,
    //     Metrics,
    //     Logs
    // }

    // private static void ConfigureOtlpExporter(OtlpExporterOptions o, OtelOptions options, Uri endpoint, bool isLambda, OtlpSignal signal)
    // {
    //     o.Protocol = ParseProtocol(options.Protocol);
    //     o.Headers = options.Headers;

    //     if (o.Protocol == OtlpExportProtocol.HttpProtobuf)
    //     {
    //         // Append proper path for HTTP if using a base URL
    //         var uriBuilder = new UriBuilder(endpoint);
    //         if (!uriBuilder.Path.EndsWith("/v1/traces") && !uriBuilder.Path.EndsWith("/v1/metrics") && !uriBuilder.Path.EndsWith("/v1/logs"))
    //         {
    //             var path = uriBuilder.Path.TrimEnd('/');
    //             string signalPath = signal switch
    //             {
    //                 OtlpSignal.Metrics => "/v1/metrics",
    //                 OtlpSignal.Logs => "/v1/logs",
    //                 _ => "/v1/traces"
    //             };
    //             uriBuilder.Path = $"{path}{signalPath}";
    //             o.Endpoint = uriBuilder.Uri;
    //         }
    //         else
    //         {
    //              o.Endpoint = endpoint;
    //         }
    //     }
    //     else
    //     {
    //         o.Endpoint = endpoint;
    //     }

    //     if (isLambda)
    //     {
    //         o.ExportProcessorType = ExportProcessorType.Simple;
    //         o.TimeoutMilliseconds = 3000;
    //     }
    // }

    // private static OtlpExportProtocol ParseProtocol(string protocol)
    // {
    //     switch (protocol.ToLowerInvariant())
    //     {
    //         case "grpc": return OtlpExportProtocol.Grpc;
    //         case "http": return OtlpExportProtocol.HttpProtobuf;
    //         default:
    //             Log.Warning("Unknown OTLP protocol '{Protocol}'. Defaulting to gRPC.", protocol);
    //             return OtlpExportProtocol.Grpc;
    //     }
    // }
}
