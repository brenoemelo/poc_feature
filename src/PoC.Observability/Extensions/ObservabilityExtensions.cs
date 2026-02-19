using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PoC.Observability.Diagnostics;

namespace PoC.Observability.Extensions;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddStartUpMetrics(this IServiceCollection services)
    {
        services.AddSingleton<StartupTimer>();
        services.AddSingleton<InstrumentationSource>();
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
        loggingBuilder.ClearProviders(); // Opcional: remove logs padrão de console do .NET para evitar duplicidade
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
                    .AddAspNetCoreInstrumentation()
                    .AddAWSInstrumentation(); // Habilitado: Essencial para propagar contexto no SQS, SNS e S3

                if (isLambda)
                {
                    tracing.AddAWSLambdaConfigurations(options =>
                    {
                        options.DisableAwsXRayContextExtraction = true; // Use OTel W3C propagation
                    });
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
                    .AddMeter(serviceName)
                    .AddHttpClientInstrumentation()
                    .AddAspNetCoreInstrumentation();

                metrics.AddOtlpExporter();
            });
    }
}
