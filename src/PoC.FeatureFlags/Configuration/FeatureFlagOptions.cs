
namespace PoC.FeatureFlags.Configuration;

public class FeatureFlagOptions
{
    public string UnleashApiUrl { get; set; } = "http://localhost:4242/api/";
    public string UnleashApiKey { get; set; } = "*:development.unleash-insecure-api-token";
    public string UnleashAppName { get; set; } = "default-app";
    public string UnleashInstanceId { get; set; } = "default-instance";
    public int FetchTogglesIntervalSeconds { get; set; } = 30;
}
