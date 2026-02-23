using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenFeature;
using OpenFeature.Model;
using PoC.FeatureFlags.Configuration;
using Unleash;
using Unleash.ClientFactory;

namespace PoC.FeatureFlags.Extensions;

public static class FeatureFlagsExtensions
{
    public static IServiceCollection AddPoCFeatureFlags(
        this IServiceCollection services,
        Action<FeatureFlagOptions> configureOptions,
        IConfiguration configuration)
    {
        var options = new FeatureFlagOptions
        {
            UnleashApiUrl = configuration["FeatureFlags:UnleashApiUrl"] ?? "http://localhost:4242/api/",
            UnleashApiKey = configuration["FeatureFlags:UnleashApiKey"] ?? "*:development.unleash-insecure-api-token",
            UnleashAppName = configuration["FeatureFlags:UnleashAppName"] ?? "default-app",
            UnleashInstanceId = configuration["FeatureFlags:UnleashInstanceId"] ?? "default-instance",
        };

        if (int.TryParse(configuration["FeatureFlags:FetchTogglesIntervalSeconds"], out var interval))
        {
            options.FetchTogglesIntervalSeconds = interval;
        }

        configureOptions(options);

        // Register Unleash Client (Internal)
        services.AddSingleton<IUnleash>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<IUnleash>>();
            logger.LogInformation("Initializing Unleash Provider. URL: {Url}. Interval: {Interval}s", options.UnleashApiUrl, options.FetchTogglesIntervalSeconds);

            if (options.UnleashApiUrl?.Contains("fake", StringComparison.OrdinalIgnoreCase) == true)
            {
                logger.LogWarning("Using FakeUnleash provider.");
                return new FakeUnleash();
            }
            logger.LogInformation("Using Real Unleash provider.");

            var settings = new UnleashSettings
            {
                AppName = options.UnleashAppName,
                InstanceTag = options.UnleashInstanceId,
                UnleashApi = new Uri(options.UnleashApiUrl ?? "http://localhost:4242/api/"),
                FetchTogglesInterval = TimeSpan.FromSeconds(options.FetchTogglesIntervalSeconds),
                CustomHttpHeaders = new Dictionary<string, string>
                {
                    { "Authorization", options.UnleashApiKey }
                }
            };

            var factory = new UnleashClientFactory();
            var client = factory.CreateClient(settings, synchronousInitialization: false);
            
            // Set OpenFeature Provider
            Api.Instance.SetProviderAsync(new UnleashFeatureProvider(client)).Wait();

            return client;
        });

        // Register OpenFeature Client for consumers
        services.AddSingleton<IFeatureClient>(sp =>
        {
            // Resolve IUnleash to ensure it's initialized and the provider is set
            sp.GetRequiredService<IUnleash>();
            return Api.Instance.GetClient();
        });

        return services;
    }
}
