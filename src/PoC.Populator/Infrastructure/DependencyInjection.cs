using Amazon.SQS;

namespace PoC.Populator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPopulatorInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
        if (string.IsNullOrEmpty(serviceUrl))
        {
            var localStackHost = Environment.GetEnvironmentVariable("LOCALSTACK_HOSTNAME") ?? "localhost";
            var edgePort = Environment.GetEnvironmentVariable("EDGE_PORT") ?? "4566";
            serviceUrl = $"http://{localStackHost}:{edgePort}";
        }

        var sqsConfig = new AmazonSQSConfig
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = "us-east-1"
        };

        services.AddSingleton<IAmazonSQS>(sp => new AmazonSQSClient(sqsConfig));

        return services;
    }
}
