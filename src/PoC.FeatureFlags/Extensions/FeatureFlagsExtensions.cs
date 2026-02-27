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

        // DEBUG: Log configuration values
        // Note: We need a logger here before we can use it in the singleton factory, 
        // but we can't easily get one. 
        // We will rely on the logger inside the factory, but we capture the options value now.
        var useFake = options.UseFakeProvider;
        var unleashUrl = options.UnleashApiUrl;

        // DEBUG: Force manual override if environment variable is present but bind failed
        var rawUseFake = configuration.GetSection("FeatureFlags")["UseFakeProvider"];
        if (!string.IsNullOrEmpty(rawUseFake) && bool.TryParse(rawUseFake, out var parsedUseFake))
        {
            options.UseFakeProvider = parsedUseFake;
        }
        else if (!string.IsNullOrEmpty(configuration["FeatureFlags:UseFakeProvider"]) && bool.TryParse(configuration["FeatureFlags:UseFakeProvider"], out var parsedUseFake2))
        {
             options.UseFakeProvider = parsedUseFake2;
        }
        else if (!string.IsNullOrEmpty(configuration["FeatureFlags__UseFakeProvider"]) && bool.TryParse(configuration["FeatureFlags__UseFakeProvider"], out var parsedUseFake3))
        {
             options.UseFakeProvider = parsedUseFake3;
        }

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

            // Check for FakeUnleash
            if (options.UseFakeProvider)
            {
                logger.LogWarning("Using FakeUnleash provider as configured.");
                var fake = new FakeUnleash();
                Api.Instance.SetProviderAsync(new UnleashFeatureProvider(fake)).Wait();
                return fake;
            }

            var settings = new UnleashSettings
            {
                AppName = options.UnleashAppName,
                UnleashApi = new Uri(options.UnleashApiUrl),
                InstanceTag = options.UnleashInstanceId,
                FetchTogglesInterval = TimeSpan.FromSeconds(options.FetchTogglesIntervalSeconds)
            };

            var factory = new UnleashClientFactory();
            var client = factory.CreateClient(settings, synchronousInitialization: true);
            
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
