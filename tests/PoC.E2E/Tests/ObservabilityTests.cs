using System.Text.RegularExpressions;
using FluentAssertions;
using PoC.E2E.Common;
using RestSharp;

namespace PoC.E2E.Tests;

/// <summary>
/// E2E tests for observability: trace context propagation, OTLP emission, and error correlation.
/// Uses the "Mock Collector Pattern" with WireMock.Net.
/// </summary>
[Collection("E2E Tests")]
public sealed class ObservabilityTests : ApiTestBase, IDisposable
{
    private static readonly Regex TraceIdPattern = new(@"^[0-9a-f]{32}$", RegexOptions.Compiled);

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // Enable materials-crud for these tests
        await FeatureManager.EnableFlagAsync("materials-crud");
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
    }

    public void Dispose()
    {
        // No additional cleanup needed
    }

    /// <summary>
    /// Scenario A: Verify that API responses contain an X-Trace-Id header in W3C format.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Api_Response_Should_Contain_TraceId_HeaderAsync()
    {
        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);

        var response = await Client.ExecuteAsync(request);

        response.Headers.Should().NotBeNull();

        var traceIdHeader = response.Headers!
            .FirstOrDefault(h => string.Equals(h.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase));

        traceIdHeader.Should().NotBeNull(
            because: "API response must include an X-Trace-Id header for observability");

        var traceIdValue = traceIdHeader!.Value?.ToString();
        traceIdValue.Should().NotBeNullOrWhiteSpace();
        TraceIdPattern.IsMatch(traceIdValue!).Should().BeTrue(
            because: $"TraceId '{traceIdValue}' must match W3C format (32 hex chars)");
    }

    /// <summary>
    /// Scenario B: Verify that the API emits OTLP trace data to the real collector and it reaches Tempo.
    /// This test queries the Tempo API to confirm trace persistence.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Api_Request_Should_Emit_Otlp_Trace_To_CollectorAsync()
    {
        // 1. Make a request to the API
        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);
        var response = await Client.ExecuteAsync(request);

        response.IsSuccessful.Should().BeTrue(
            because: $"the API call must succeed. Status: {response.StatusCode}");

        // 2. Extract TraceId from response header
        var traceIdHeader = response.Headers!
            .FirstOrDefault(h => string.Equals(h.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase));
        
        traceIdHeader.Should().NotBeNull("API response must contain X-Trace-Id header");
        var traceId = traceIdHeader!.Value?.ToString();
        traceId.Should().NotBeNullOrWhiteSpace("TraceId must be valid");

        // 3. Poll Tempo API to verify trace existence
        // Tempo is exposed on port 3200 in docker-compose
        var tempoClient = new RestClient("http://localhost:3200");
        var tempoRequest = new RestRequest($"/api/traces/{traceId}", Method.Get);
        
        // Retry loop for eventual consistency (Collector -> Batch -> Tempo)
        bool traceFound = false;
        for (int i = 0; i < 10; i++)
        {
            var tempoResponse = await tempoClient.ExecuteAsync(tempoRequest);
            if (tempoResponse.IsSuccessful && tempoResponse.Content!.Contains(traceId!))
            {
                traceFound = true;
                break;
            }

            await Task.Delay(1000); // Wait 1s before retry
        }

        traceFound.Should().BeTrue(
            because: $"Trace {traceId} should be visible in Tempo (http://localhost:3200) within 10 seconds");
    }

    /// <summary>
    /// Scenario C: Verify that a 404 error response still includes trace correlation headers.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Error_Response_Should_Contain_TraceId_HeaderAsync()
    {
        var nonExistentId = $"observability-test-{Guid.NewGuid():N}";
        var request = new RestRequest(
            $"/api/v1/materials/{nonExistentId}",
            Method.Get);

        var response = await Client.ExecuteAsync(request);

        response.StatusCode.Should().Be(
            System.Net.HttpStatusCode.NotFound,
            because: "requesting a non-existent material should return 404");

        var traceIdHeader = response.Headers?
            .FirstOrDefault(h => string.Equals(h.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase));

        traceIdHeader.Should().NotBeNull(
            because: "even error responses must include X-Trace-Id for debugging and correlation");

        var traceIdValue = traceIdHeader!.Value?.ToString();
        traceIdValue.Should().NotBeNullOrWhiteSpace();
        TraceIdPattern.IsMatch(traceIdValue!).Should().BeTrue(
            because: $"TraceId '{traceIdValue}' must match W3C format (32 hex chars)");
    }

    /// <summary>
    /// Scenario D: Verify that Grafana (Loki) successfully receives telemetry logs over OTLP.
    /// This requires the local docker compose stack to be running (Loki at localhost:3100).
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Grafana_Should_Receive_TelemetryAsync()
    {
        // 1. Generate real traffic on the Materials API to trigger OTel emissions
        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);
        var response = await Client.ExecuteAsync(request);
        
        // We don't assert IsSuccessful because Unleash sync delay might return 404.
        // Whether it's 200 or 404, the API still emits a trace!
        var traceIdHeader = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase));
        var traceId = traceIdHeader?.Value?.ToString();
        traceId.Should().NotBeNullOrWhiteSpace(because: "Trace ID should be returned to trace in Grafana");

        // Allow some buffer for the OpenTelemetry Collector's batch processor to flush (default ~5s-10s) + Loki ingestion
        await Task.Delay(TimeSpan.FromSeconds(15));

        // 2. Query Loki for any logs tagged with this trace ID
        var lokiClient = new RestClient("http://localhost:3100");
        
        // Typical OTel -> Loki mapping uses job as service.name
        var query = $"{{job=\"PoC-Materials\"}} |= `{traceId}`";
        var lokiRequest = new RestRequest($"/loki/api/v1/query?query={Uri.EscapeDataString(query)}", Method.Get);

        var lokiResponse = await lokiClient.ExecuteAsync(lokiRequest);

        lokiResponse.IsSuccessful.Should().BeTrue(because: $"Loki should be reachable at localhost:3100. Error: {lokiResponse.ErrorMessage}");
        lokiResponse.Content.Should().Contain(traceId, because: $"Loki should have ingested logs containing the Trace ID '{traceId}' from the PoC-Materials service via the OTel Collector.");
    }

    /// <summary>
    /// Scenario E: Verify that Grafana (Tempo) successfully receives telemetry traces over OTLP.
    /// This requires the local docker compose stack to be running (Tempo at localhost:3200).
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task Grafana_Should_Receive_TraceAsync()
    {
        // 1. Generate real traffic on the Materials API to trigger OTel emissions
        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);
        var response = await Client.ExecuteAsync(request);
        
        var traceIdHeader = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase));
        var traceId = traceIdHeader?.Value?.ToString();
        traceId.Should().NotBeNullOrWhiteSpace(because: "Trace ID should be returned to trace in Grafana");

        // Allow some buffer for the OpenTelemetry Collector's batch processor to flush (default ~5s-10s) + Tempo ingestion
        await Task.Delay(TimeSpan.FromSeconds(15));

        // 2. Query Tempo for the trace
        var tempoClient = new RestClient("http://localhost:3200");
        
        var tempoRequest = new RestRequest($"/api/traces/{traceId}", Method.Get);

        var tempoResponse = await tempoClient.ExecuteAsync(tempoRequest);

        tempoResponse.IsSuccessful.Should().BeTrue(because: $"Tempo should be reachable at localhost:3200 and find the trace '{traceId}'. Error: {tempoResponse.ErrorMessage}");
        tempoResponse.Content.Should().Contain(traceId, because: $"Tempo should have ingested a trace containing the Trace ID '{traceId}' from the PoC-Materials service via the OTel Collector.");
    }
}
