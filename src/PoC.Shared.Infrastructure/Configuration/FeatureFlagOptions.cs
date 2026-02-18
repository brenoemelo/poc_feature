namespace PoC.Shared.Infrastructure.Configuration;

/// <summary>
/// Configuration options for Feature Flags (OpenFeature + GO Feature Flag).
/// </summary>
public sealed record FeatureFlagOptions
{
    /// <summary>
    /// Gets the GO Feature Flag relay proxy endpoint.
    /// </summary>
    public string Endpoint { get; init; } = "http://gofeatureflag:1031";

    /// <summary>
    /// Gets the application name used as the OpenFeature provider domain.
    /// </summary>
    public string AppName { get; init; } = "poc-app";

    /// <summary>
    /// Gets the timeout in seconds for the provider connection.
    /// </summary>
    public int TimeoutSeconds { get; init; } = 5;
}
