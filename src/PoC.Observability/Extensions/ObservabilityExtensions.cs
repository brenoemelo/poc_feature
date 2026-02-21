using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
// using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
            tracerProvider?.ForceFlush();

            var meterProvider = services.GetService<MeterProvider>();
            meterProvider?.ForceFlush();

            var loggerProvider = services.GetService<LoggerProvider>();
            loggerProvider?.ForceFlush();
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
        ConfigureObservability(builder.Services, builder.Logging, serviceName, serviceVersion);
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
        ConfigureObservability(builder.Services, builder.Logging, serviceName, serviceVersion);
        return builder;
    }

    private static void ConfigureObservability(
        IServiceCollection services,
        ILoggingBuilder loggingBuilder,
        string serviceName,
        string serviceVersion)
    {
        var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

        // 0. Add Startup Metrics
        services.AddStartUpMetrics();

        // 1. Definir o Resource Builder centralizado para garantir metadados idênticos em Logs, Metrics e Traces
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion);

        // 2. Configure Logging (Substituindo o Serilog pelo ILogger Nativo Integrado ao OTel)
        // loggingBuilder.ClearProviders(); // Keep default providers (Console) for debugging in Lambda
        loggingBuilder.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resourceBuilder);
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            logging.AddOtlpExporter();
        });

        // 3. Configure OpenTelemetry (Tracing & Metrics)
        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(serviceName)
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation();
                    //.AddAWSInstrumentation(); // Habilitado: Essencial para propagar contexto no SQS, SNS e S3

                if (isLambda)
                {
                    /*
                    tracing.AddAWSLambdaConfigurations(options =>
                    {
                        options.DisableAwsXRayContextExtraction = true; // Use OTel W3C propagation
                    });
                    */
                }

                tracing.AddOtlpExporter(options =>
                {
                    if (isLambda)
                    {
                        // Em Lambdas, o processador "Simple" garante o envio imediato para o sidecar
                        // antes que a AWS congele o ambiente de execução.
                        options.ExportProcessorType = ExportProcessorType.Simple;
                    }
                });
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddRuntimeInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(serviceName) // Métricas de Negócio
                    .AddMeter("app.startup") // Métricas de Startup
                    .AddConsoleExporter() // Debug: Ver se métricas são geradas
                    .AddOtlpExporter(options =>
                    {
                        if (isLambda)
                    {
                        // Para Lambda, usamos o endpoint HTTP/Protobuf para evitar problemas com gRPC
                        // A configuração vem das variáveis de ambiente (OTEL_EXPORTER_OTLP_ENDPOINT/PROTOCOL)
                        options.ExportProcessorType = ExportProcessorType.Simple;
                    }
                    });
            });
    }
}
