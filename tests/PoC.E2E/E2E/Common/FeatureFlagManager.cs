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
        // Unleash API expects a JSON body. The init script sends { "enabled": true } for 'on'.
        // For 'off', it likely expects { "enabled": false } or just empty object, but let's be consistent.
        request.AddJsonBody(new { enabled = isEnabled });

        var response = await _unleashClient.ExecuteAsync(request);

        if (!response.IsSuccessful)
        {
            throw new Exception($"Failed to set flag '{flagKey}' to '{targetState}'. Status: {response.StatusCode}, Content: {response.Content}");
        }

        _modifiedFlags.Add(flagKey);

        Console.WriteLine($"[FeatureFlagManager] Flag '{flagKey}' successfully updated to '{targetState}'.");
    }
}
