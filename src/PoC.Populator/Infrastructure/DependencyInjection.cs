using Amazon.SQS;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PoC.Populator.Configuration;
using PoC.Populator.Domain.Interfaces;
using PoC.Populator.Domain.Models;
using PoC.Populator.Domain.Services;
using PoC.Populator.Domain.Validators;
using PoC.Populator.Functions;
using PoC.Populator.Infrastructure.Services;
using PoC.Shared.Models;

namespace PoC.Populator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPopulatorInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssemblyContaining<PopulationRequestValidator>();

        services.AddOptions<AwsOptions>()
            .Bind(configuration.GetSection(AwsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ServiceOptions>()
            .Bind(configuration.GetSection(ServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PopulatorOptions>()
            .Bind(configuration.GetSection(PopulatorOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonSQS>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
            var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") ?? opts.ServiceUrl;

            var sqsConfig = new AmazonSQSConfig
            {
                ServiceURL = serviceUrl,
                AuthenticationRegion = opts.Region
            };
            return new AmazonSQSClient(sqsConfig);
        });
        
        services.AddSingleton<MaterialPopulationStrategy>();
        services.AddSingleton<PricePopulationStrategy>();
        services.AddSingleton<EnsurePricesPopulationStrategy>();
        
        services.AddScoped<IPopulationService, PopulationService>();

        return services;
    }
}
