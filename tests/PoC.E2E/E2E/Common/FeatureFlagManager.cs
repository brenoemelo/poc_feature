using RestSharp;

namespace PoC.E2E.Common;

public class FeatureFlagManager
{
    private readonly RestClient _unleashClient;
    private readonly HashSet<string> _modifiedFlags = new();

    public FeatureFlagManager(string unleashApiUrl, string adminToken)
    {
        var options = new RestClientOptions(unleashApiUrl)
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        _unleashClient = new RestClient(options);
        _unleashClient.AddDefaultHeader("Authorization", adminToken);
        _unleashClient.AddDefaultHeader("Content-Type", "application/json");
    }

    public async Task EnableFlagAsync(string flagKey)
    {
        await SetFlagStateAsync(flagKey, true);
    }

    public async Task DisableFlagAsync(string flagKey)
    {
        await SetFlagStateAsync(flagKey, false);
    }

    public async Task ResetAllTrackedFlagsAsync()
    {
        var flags = _modifiedFlags.ToList();
        _modifiedFlags.Clear(); // Clear first to avoid re-adding during ResetFlagAsync
        
        foreach (var flagKey in flags)
        {
            await ResetFlagAsync(flagKey);
        }
    }

    public async Task ResetFlagAsync(string flagKey)
    {
        // Default to enabled for this PoC
        await SetFlagStateAsync(flagKey, true);
        _modifiedFlags.Remove(flagKey);
    }

    private async Task SetFlagStateAsync(string flagKey, bool isEnabled)
    {
        var targetState = isEnabled ? "on" : "off";
        Console.WriteLine($"[FeatureFlagManager] Setting flag '{flagKey}' to '{targetState}'...");

        var request = new RestRequest($"admin/projects/default/features/{flagKey}/environments/development/{targetState}", Method.Post);
        // Unleash API expects a JSON body even if empty for some endpoints, but for toggle on/off it might just need the endpoint.
        // Documentation says: POST /api/admin/projects/:projectId/features/:featureName/environments/:environment/on
        // Body: {} (optional?) - Let's send empty json just in case.
        request.AddJsonBody(new { });

        var response = await _unleashClient.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            throw new Exception($"Failed to set flag '{flagKey}' to '{targetState}'. Status: {response.StatusCode}, Content: {response.Content}");
        }

        _modifiedFlags.Add(flagKey);

        // Wait for propagation (poll interval)
        // Unleash client in the app polls every X seconds (default is often 15s, but we might have configured it lower or it uses push?)
        // In this PoC, we might need to wait a bit.
        Console.WriteLine("[FeatureFlagManager] Waiting 2s for app provider to poll changes...");
        await Task.Delay(2000);

        Console.WriteLine($"[FeatureFlagManager] Flag '{flagKey}' successfully updated to '{targetState}'.");
    }
}
