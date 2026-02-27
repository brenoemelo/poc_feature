using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Amazon.DynamoDBv2;
using PoC.Costing.Configuration;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Infrastructure.Persistence;

namespace PoC.Costing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCostingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AwsOptions>()
            .Bind(configuration.GetSection(AwsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
            
        services.AddOptions<ServiceOptions>()
            .Bind(configuration.GetSection(ServiceOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonDynamoDB>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AwsOptions>>().Value;
            var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL") ?? opts.ServiceUrl;

            var config = new AmazonDynamoDBConfig
            {
                ServiceURL = serviceUrl,
                AuthenticationRegion = opts.Region
            };
            return new AmazonDynamoDBClient(config);
        });

        services.AddOptions<CostingOptions>()
            .Bind(configuration.GetSection(CostingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<ICostingRepository, DynamoDbCostingRepository>();

        return services;
    }
}
