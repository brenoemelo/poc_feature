namespace PoC.Shared.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Feature Flags using OpenFeature providers.
/// </summary>
public sealed record FeatureFlagOptions
{
    /// <summary>
    /// Gets the provider name. Supported: "Unleash".
    /// </summary>
    public string Provider { get; init; } = "Unleash";

    /// <summary>
    /// Gets the application name used as the OpenFeature provider domain.
    /// </summary>
    public string AppName { get; init; } = "poc-app";

    /// <summary>
    /// Gets the timeout in seconds for the provider connection.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 5;

    /// <summary>
    /// Gets the Unleash API URL.
    /// </summary>
    public string UnleashApiUrl { get; init; } = "http://unleash:4242/api/";

    /// <summary>
    /// Gets the Unleash API Key.
    /// </summary>
    public string? UnleashApiKey { get; init; }

    /// <summary>
    /// Gets the Unleash App Name.
    /// </summary>
    public string UnleashAppName { get; init; } = "poc-app";

    /// <summary>
    /// Gets the Unleash Instance ID.
    /// </summary>
    public string UnleashInstanceId { get; init; } = "poc-instance";
}
