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
        IConfiguration configuration,
        Action<FeatureFlagOptions>? configureOptions = null)
    {
        var options = new FeatureFlagOptions();
        
        // 1. Bind from Configuration (appsettings.json + Environment Variables)
        configuration.GetSection("FeatureFlags").Bind(options);

        // 2. Allow manual overrides
        configureOptions?.Invoke(options);

        // 3. Validation
        if (string.IsNullOrEmpty(options.UnleashApiUrl))
        {
             // Log warning or throw? For now, we assume it's configured.
        }

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
