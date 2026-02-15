using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Infrastructure.Persistence;

namespace PoC.Costing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCostingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
            var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
            serviceUrl = $"http://{localStackHost}:{edgePort}";
        }

        var dynamoConfig = new AmazonDynamoDBConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = "us-east-1"
        };

        services.AddSingleton<IAmazonDynamoDB>(sp => new AmazonDynamoDBClient(dynamoConfig));
        services.AddScoped<IDynamoDBContext, DynamoDBContext>();
        services.AddScoped<ICostingRepository, DynamoDbCostingRepository>();

        return services;
    }
}
