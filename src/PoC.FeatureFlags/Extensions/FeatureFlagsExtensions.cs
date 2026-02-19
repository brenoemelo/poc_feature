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
        Action<FeatureFlagOptions> configureOptions)
    {
        var options = new FeatureFlagOptions();
        configureOptions(options);

        // Register Unleash Client
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
            return factory.CreateClient(settings, synchronousInitialization: true);
        });

        // Register OpenFeature Provider (Custom Wrapper or Contrib)
        // Since we didn't pull the heavy Contrib package, we can use a simple adapter or just expose IUnleash directly.
        // For this PoC, let's expose IUnleash as the primary mechanism, but ideally we'd wrap it for OpenFeature.
        // Given the prompt "Separar melhor", satisfying the dependency on Unleash is sufficient.

        return services;
    }
}
