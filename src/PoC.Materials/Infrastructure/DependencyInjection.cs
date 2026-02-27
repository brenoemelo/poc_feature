using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Amazon.DynamoDBv2;
using PoC.Materials.Configuration;
using PoC.Materials.Domain.Interfaces;
using PoC.Materials.Infrastructure.Persistence;

namespace PoC.Materials.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMaterialsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AwsOptions>()
            .Bind(configuration.GetSection(AwsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<MaterialsOptions>()
            .Bind(configuration.GetSection(MaterialsOptions.SectionName))
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

        services.AddScoped<IMaterialRepository, DynamoDbMaterialRepository>();
        services.AddSingleton<MaterialsMetrics>();

        return services;
    }
}
