using System.Diagnostics;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using PoC.E2E.Common;
using RestSharp;
using Xunit.Abstractions;

namespace PoC.E2E.Tests;

[Collection("E2E Tests")]
public class FlagPropagationTests : ApiTestBase
{
    private readonly ITestOutputHelper _output;

    public FlagPropagationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Measure_Flag_Propagation_Time_Via_Api_Gateway_Async()
    {
        // Arrange
        var flagKey = "materials-crud";
        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);
        
        // 1. Check current status via API Gateway
        var initialResponse = await Client.ExecuteAsync(request);
        bool isCurrentlyEnabled = initialResponse.StatusCode == HttpStatusCode.OK;
        
        _output.WriteLine($"[Test 1] Initial state from API Gateway: {initialResponse.StatusCode} ({(isCurrentlyEnabled ? "Enabled" : "Disabled")})");

        var targetEnabled = !isCurrentlyEnabled;
        _output.WriteLine($"[Test 1] Toggling to: {(targetEnabled ? "Enabled" : "Disabled")}");

        // Act: Toggle flag
        if (targetEnabled)
        {
            await FeatureManager.EnableFlagAsync(flagKey);
        }
        else
        {
            await FeatureManager.DisableFlagAsync(flagKey);
        }

        // Measure propagation time
        var stopwatch = Stopwatch.StartNew();
        var changed = false;
        // Default FetchTogglesInterval is 30s in FeatureFlagOptions.cs
        // Unleash docs say default is often 15s, but our config is 30s.
        // We need > 30s to ensure the client has polled the new state.
        var maxRetries = 45; 

        for (int i = 0; i < maxRetries; i++)
        {
            await Task.Delay(1000); // Wait 1 second
            
            var response = await Client.ExecuteAsync(request);
            bool currentStatus = response.StatusCode == HttpStatusCode.OK;
            
            if (currentStatus == targetEnabled)
            {
                changed = true;
                break;
            }
            
            _output.WriteLine($"[Test 1] Attempt {i + 1}: Flag still {(isCurrentlyEnabled ? "Enabled" : "Disabled")} ({response.StatusCode}). Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s");
        }

        stopwatch.Stop();

        // Assert
        changed.Should().BeTrue($"Flag '{flagKey}' did not propagate to API Gateway within {maxRetries} seconds.");
        
        _output.WriteLine($"[Test 1] Success! Flag propagation took {stopwatch.Elapsed.TotalSeconds:F2}s");
    }

    [Fact]
    public async Task Measure_Flag_Propagation_Time_Via_Unleash_Client_Api_Async()
    {
        // Arrange
        var flagKey = "materials-crud";
        
        // Setup Unleash Client API
        var unleashClientApiUrl = Config.GetValue<string>("UnleashApiUrl")?.TrimEnd('/');
        var clientRequest = new RestRequest($"{unleashClientApiUrl}/client/features", Method.Get);
        // Add required headers for Unleash Client API
        clientRequest.AddHeader("Authorization", Config.GetValue<string>("UnleashAdminToken") ?? "*:*.admin-token");
        
        var client = new RestClient();
        
        // 1. Get ACTUAL VALUE
        var initialResponse = await client.ExecuteAsync(clientRequest);
        initialResponse.IsSuccessful.Should().BeTrue("Should be able to query Unleash Client API");
        
        var initialContent = initialResponse.Content;
        initialContent.Should().Contain($"\"{flagKey}\"");
        
        bool isCurrentlyEnabled = GetFlagStatusFromJson(initialContent!, flagKey);
        _output.WriteLine($"[Test 2] Initial state from Unleash: {(isCurrentlyEnabled ? "Enabled" : "Disabled")}");

        // 2. Invert it
        var targetEnabled = !isCurrentlyEnabled;
        _output.WriteLine($"[Test 2] Toggling to: {(targetEnabled ? "Enabled" : "Disabled")}");

        // Act: Toggle flag
        if (targetEnabled)
        {
            await FeatureManager.EnableFlagAsync(flagKey);
        }
        else
        {
            await FeatureManager.DisableFlagAsync(flagKey);
        }

        // Measure propagation time
        var stopwatch = Stopwatch.StartNew();
        var changed = false;
        var maxRetries = 15;

        for (int i = 0; i < maxRetries; i++)
        {
            await Task.Delay(1000); // Wait 1 second
            
            var response = await client.ExecuteAsync(clientRequest);
            var content = response.Content;
            
            if (content != null)
            {
                bool currentStatus = GetFlagStatusFromJson(content, flagKey);
                if (currentStatus == targetEnabled)
                {
                    changed = true;
                    break;
                }
            }
            
            _output.WriteLine($"[Test 2] Attempt {i + 1}: Flag still {(isCurrentlyEnabled ? "Enabled" : "Disabled")} in Unleash API. Elapsed: {stopwatch.Elapsed.TotalSeconds:F2}s");
        }

        stopwatch.Stop();

        // Assert
        changed.Should().BeTrue($"Flag '{flagKey}' did not propagate to Unleash Client API within {maxRetries} seconds.");
        
        _output.WriteLine($"[Test 2] Success! Flag propagation took {stopwatch.Elapsed.TotalSeconds:F2}s");
    }

    private bool GetFlagStatusFromJson(string json, string flagKey)
    {
        try 
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var features = doc.RootElement.GetProperty("features");
            foreach (var feature in features.EnumerateArray())
            {
                if (feature.GetProperty("name").GetString() == flagKey)
                {
                    return feature.GetProperty("enabled").GetBoolean();
                }
            }

            // If not found, assume disabled or throw? 
            // Better to throw to be explicit in tests, but for robustness let's log and return false
            _output.WriteLine($"Flag {flagKey} not found in response.");
            return false;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"Error parsing JSON or finding flag: {ex.Message}");
            return false;
        }
    }
}
