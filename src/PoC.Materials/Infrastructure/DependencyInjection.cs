using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using PoC.Materials.Domain.Interfaces;
using PoC.Materials.Infrastructure.Persistence;

namespace PoC.Materials.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMaterialsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAWSService<IAmazonDynamoDB>();

        services.AddScoped<IDynamoDBContext, DynamoDBContext>();
        services.AddScoped<IMaterialRepository, DynamoDbMaterialRepository>();

        return services;
    }
}
