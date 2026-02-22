using Amazon.SQS;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PoC.Populator.Domain.Services;
using PoC.Populator.Functions;
using PoC.Populator.Domain.Interfaces;
using PoC.Populator.Infrastructure.Services;
using PoC.Shared.Validators;

namespace PoC.Populator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPopulatorInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssemblyContaining<PopulationRequestValidator>();

        services.Configure<PopulatorOptions>(options =>
        {
            configuration.GetSection(PopulatorOptions.SectionName).Bind(options);

            if (string.IsNullOrEmpty(options.QueueUrl))
            {
                var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
                if (string.IsNullOrEmpty(serviceUrl))
                {
                    var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
                    var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
                    serviceUrl = $"http://{localStackHost}:{edgePort}";
                }
                
                var queueName = Environment.GetEnvironmentVariable("QUEUE_NAME") ?? "populator-queue";
                options.QueueUrl = $"{serviceUrl}/000000000000/{queueName}";
            }
        });

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<PopulatorOptions>>().Value;
            var sqsConfig = new AmazonSQSConfig
            {
                ServiceURL = opts.QueueUrl.Substring(0, opts.QueueUrl.IndexOf("/000000000000", StringComparison.Ordinal)), // Extract base URL
                AuthenticationRegion = "us-east-1"
            };
            
            // Fallback if substring fails
            if (string.IsNullOrEmpty(sqsConfig.ServiceURL))
            {
                 sqsConfig.ServiceURL = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") ?? "http://localhost:4566";
            }
            return new AmazonSQSClient(sqsConfig);
        });
        
        services.AddSingleton<MaterialPopulationStrategy>();
        services.AddSingleton<PricePopulationStrategy>();
        services.AddSingleton<EnsurePricesPopulationStrategy>();
        
        services.AddScoped<IPopulationService, PopulationService>();

        return services;
    }
}
