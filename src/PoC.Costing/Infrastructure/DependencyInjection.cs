using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using PoC.Costing.Domain.Interfaces;
using PoC.Costing.Infrastructure.Persistence;

namespace PoC.Costing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCostingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var endpoint = configuration["AWS_ENDPOINT_URL"] ?? Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        
        if (string.IsNullOrEmpty(endpoint))
        {
             var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME");
             if (!string.IsNullOrEmpty(localStackHost))
             {
                 var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
                 endpoint = $"http://{localStackHost}:{edgePort}";
             }
        }

        if (!string.IsNullOrEmpty(endpoint))
        {
            Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", endpoint);
        }

        services.AddAWSService<IAmazonDynamoDB>();

        services.AddScoped<IDynamoDBContext, DynamoDBContext>();
        services.AddScoped<ICostingRepository, DynamoDbCostingRepository>();

        return services;
    }
}
