using System.Net;
using FluentAssertions;
using PoC.E2E.Common;
using RestSharp;

namespace PoC.E2E.Tests;

[Collection("E2E Tests")]
public class FeatureFlagE2ETests : ApiTestBase
{
    [Fact]
    public async Task FeatureGate_Should_Block_Access_When_Flag_Is_DisabledAsync()
    {
        // Arrange
        var flagKey = "materials-crud"; // Using an existing flag
        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);

        // Act 1: Disable Flag
        await FeatureManager.DisableFlagAsync(flagKey);

        // Assert 1: Retry until 404 (Lambda might need to wake up and poll)
        await AssertEventuallyAsync(
            async () => await Client.ExecuteAsync(request),
            HttpStatusCode.NotFound,
            $"flag '{flagKey}' is disabled, so endpoint should return 404",
            timeoutSeconds: 45);

        // Act 2: Enable Flag
        await FeatureManager.EnableFlagAsync(flagKey);

        // Assert 2: Retry until 200
        await AssertEventuallyAsync(
            async () => await Client.ExecuteAsync(request),
            HttpStatusCode.OK,
            $"flag '{flagKey}' is enabled, so endpoint should work",
            timeoutSeconds: 45);
    }

    private async Task AssertEventuallyAsync(
        Func<Task<RestResponse>> act,
        HttpStatusCode expectedStatusCode,
        string failReason,
        int timeoutSeconds = 15)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RestResponse? lastResponse = null;

        while (sw.Elapsed.TotalSeconds < timeoutSeconds)
        {
            lastResponse = await act();
            if (lastResponse.StatusCode == expectedStatusCode)
            {
                return; // Success
            }

            await Task.Delay(1000); // Wait 1s between attempts to allow Lambda to process background tasks
        }

        // Final assertion
        lastResponse?.StatusCode.Should().Be(expectedStatusCode, failReason);
    }
}
