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

    private readonly OtlpMockServer _mockServer;

    public ObservabilityTests()
    {
        var port = int.Parse(Config["OtlpMockPort"] ?? "14318");
        _mockServer = new OtlpMockServer(port);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // Enable materials-crud for these tests
        await FeatureManager.EnableFlagAsync("materials-crud");
    }

    public override async Task DisposeAsync()
    {
        _mockServer.Dispose();
        await base.DisposeAsync();
    }

    public void Dispose()
    {
        _mockServer.Dispose();
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
    /// Scenario B: Verify that the API emits OTLP trace data to the mock collector.
    /// Requires the Lambda to be deployed with Otel__Endpoint pointing to this mock server.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact(Skip = "Incompatible with LocalStack deployment. The Lambda is hardcoded to send traces to the docker-network otel-collector, not the host-network MockServer.")]
    public async Task Api_Request_Should_Emit_Otlp_Trace_To_CollectorAsync()
    {
        _mockServer.Reset();

        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);
        var response = await Client.ExecuteAsync(request);

        response.IsSuccessful.Should().BeTrue(
            because: $"the API call must succeed before checking traces. Status: {response.StatusCode}, Content: {response.Content}");

        var received = await _mockServer.WaitForTraceAsync(TimeSpan.FromSeconds(10));

        received.Should().BeTrue(
            because: "the mock OTLP collector should have received at least one trace export. " +
                     "Ensure the Lambda is deployed with Otel__Endpoint=http://host.docker.internal:{_mockServer.Port} and Otel__Protocol=http");

        var payloads = _mockServer.GetTracePayloadsAsString();
        payloads.Should().NotBeEmpty(because: "trace payloads should have been captured");
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
}
