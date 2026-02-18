using System.Text.Json;
using Polly;
using Polly.Retry;
using RestSharp;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PoC.E2E.Common;

public class FeatureFlagManager
{
    private readonly string _flagsFilePath;
    private readonly RestClient _goffClient;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;
    private readonly HashSet<string> _modifiedFlags = new();
    private readonly AsyncRetryPolicy _retryPolicy;

    public FeatureFlagManager(string flagsFilePath, string goffBaseUrl = "http://localhost:1031")
    {
        _flagsFilePath = flagsFilePath;
        _goffClient = new RestClient(goffBaseUrl);

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        // Retry policy for flag propagation check
        _retryPolicy = Policy
            .Handle<Exception>()
            .Or<InvalidOperationException>() // Custom exception for check failure
            .WaitAndRetryAsync(
                80, // Increased to 80s to accommodate potential 60s polling interval
                retryAttempt => TimeSpan.FromSeconds(1),
                (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine($"[FeatureFlagManager] Retry {retryCount} waiting for flag propagation: {exception.Message}");
                });
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
        // Default to enabled for this PoC as per flags.yaml default state
        await SetFlagStateAsync(flagKey, true);
        _modifiedFlags.Remove(flagKey);
    }

    private async Task SetFlagStateAsync(string flagKey, bool isEnabled)
    {
        var targetVariation = isEnabled ? "enabled" : "disabled";

        Console.WriteLine($"[FeatureFlagManager] Setting flag '{flagKey}' to '{targetVariation}'...");

        // 1. Modify the file
        await ModifyFlagFileAsync(flagKey, targetVariation);
        _modifiedFlags.Add(flagKey);

        // 2. Wait for propagation (poll GoFeatureFlag Proxy via OFREP)
        await WaitForFlagPropagationAsync(flagKey, isEnabled);
        
        // 3. Wait for provider in app to poll the proxy (polling interval is 1s)
        Console.WriteLine("[FeatureFlagManager] Waiting 2s for app provider to poll proxy...");
        await Task.Delay(2000);

        Console.WriteLine($"[FeatureFlagManager] Flag '{flagKey}' successfully updated to '{targetVariation}'.");
    }

    // Structure: key -> { defaultRule -> { variation: "xxx" }, ... }
    private async Task ModifyFlagFileAsync(string flagKey, string variation)
    {
        // Read file content
        var content = await File.ReadAllTextAsync(_flagsFilePath);
        
        // Deserialize to dynamic/dictionary structure to preserve other fields
        var flags = _yamlDeserializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(content);

        if (!flags.ContainsKey(flagKey))
        {
            throw new KeyNotFoundException($"Flag '{flagKey}' not found in {_flagsFilePath}");
        }

        var flagData = flags[flagKey];
        
        // Update defaultRule variation
        if (flagData.TryGetValue("defaultRule", out var defaultRuleObj))
        {
            if (defaultRuleObj is Dictionary<object, object> defaultRuleDict)
            {
                defaultRuleDict["variation"] = variation;
            }
            else if (defaultRuleObj is Dictionary<string, object> defaultRuleStringDict)
            {
                defaultRuleStringDict["variation"] = variation;
            }
        }
        
        // Update variations if needed (optional, usually variation name is enough)
        // Note: We assume variations 'enabled' and 'disabled' exist in the file.

        // Serialize back to YAML
        var newContent = _yamlSerializer.Serialize(flags);
        await File.WriteAllTextAsync(_flagsFilePath, newContent);
    }

    private async Task WaitForFlagPropagationAsync(string flagKey, bool expectedState)
    {
        await _retryPolicy.ExecuteAsync(async () =>
        {
            // Use OFREP evaluation endpoint
            var request = new RestRequest("/ofrep/v1/evaluate/flags", Method.Post);
            request.AddJsonBody(new
            {
                context = new
                {
                    targetingKey = "e2e-test-verifier"
                }
            });

            var response = await _goffClient.ExecuteAsync(request);
            if (!response.IsSuccessful)
            {
                throw new Exception($"Failed to query OFREP API: {response.StatusCode} - {response.Content}");
            }

            Console.WriteLine($"[FeatureFlagManager] OFREP Response: {response.Content}");

            // Parse response manually or use a model
            // OFREP response: { "flags": [ { "key": "...", "value": true/false, ... } ] }
            using var doc = JsonDocument.Parse(response.Content!);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("flags", out var flagsElement) && flagsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var flag in flagsElement.EnumerateArray())
                {
                    if (flag.TryGetProperty("key", out var keyProp) &&
                        keyProp.GetString() == flagKey &&
                        flag.TryGetProperty("value", out var valueProp))
                    {
                        bool actualValue;
                        if (valueProp.ValueKind == JsonValueKind.True)
                        {
                            actualValue = true;
                        }
                        else if (valueProp.ValueKind == JsonValueKind.False)
                        {
                            actualValue = false;
                        }
                        else
                        {
                            throw new Exception($"Flag '{flagKey}' value is not boolean: {valueProp.ValueKind}");
                        }

                        if (actualValue != expectedState)
                        {
                            throw new InvalidOperationException($"Flag '{flagKey}' value mismatch. Expected: {expectedState}, Actual: {actualValue}");
                        }

                        return; // Success
                    }
                }

                throw new Exception($"Flag '{flagKey}' not found in OFREP response");
            }
            else
            {
                throw new Exception("Invalid OFREP response format (missing 'flags' array)");
            }
        });
    }
}
