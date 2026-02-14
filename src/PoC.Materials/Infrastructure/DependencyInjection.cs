using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using PoC.Materials.Repositories;

namespace PoC.Materials.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMaterialsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceUrl = configuration.GetValue<string>("AWS:ServiceURL")
            ?? Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");

        if (!string.IsNullOrEmpty(serviceUrl))
        {
            services.AddSingleton<IAmazonDynamoDB>(_ =>
            {
                var config = new AmazonDynamoDBConfig
                {
                    ServiceURL = serviceUrl,
                    AuthenticationRegion = configuration.GetValue<string>("AWS:Region") ?? "us-east-1"
                };
                return new AmazonDynamoDBClient(config);
            });
        }
        else
        {
            services.AddAWSService<IAmazonDynamoDB>();
        }

        services.AddScoped<IDynamoDBContext, DynamoDBContext>();
        services.AddScoped<IMaterialRepository, DynamoDbMaterialRepository>();

        return services;
    }
}
