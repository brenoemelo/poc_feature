using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using PoC.Materials.Domain.Interfaces;
using PoC.Materials.Infrastructure.Persistence;

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
                var accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test";
                var secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test";
                var credentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonDynamoDBClient(credentials, config);
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
