using Microsoft.Extensions.Configuration;
using RestSharp;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace PoC.Observability.E2E;

public class PrometheusTests
{
    private readonly ObservabilityClient _observabilityClient;
    private readonly RestClient _appClient;
    private readonly ITestOutputHelper _output;

    public PrometheusTests(ITestOutputHelper output)
    {
        _output = output;

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json")
            .Build();

        _observabilityClient = new ObservabilityClient(configuration);
        
        var appUrl = configuration["Observability:AppBaseUrl"] 
                     ?? throw new ArgumentNullException("Observability:AppBaseUrl");
        _appClient = new RestClient(appUrl);
    }

    [Fact]
    public async Task Prometheus_Should_Be_Up_And_Running()
    {
        // Act
        // Verify Prometheus itself is reachable via the client
        // We can query for "up" metric which should return results for our targets
        var query = "up";
        var result = await _observabilityClient.QueryPrometheusAsync(query);

        // Assert
        result.Should().NotBeNullOrEmpty("Prometheus query result should not be empty");
        result.Should().Contain("\"status\":\"success\"", "Response should indicate success");
        result.Should().Contain("otel-collector", "Should contain otel-collector target");
    }

    [Fact]
    public async Task Metrics_Should_Be_Ingested_And_Queryable()
    {
        // 1. Generate traffic to ensure metrics are created this session
        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);
        var response = await _appClient.ExecuteAsync(request);
        
        // Unleash async loading might return 404 originally, but a metric is still emitted.

        // Wait for OpenTelemetry Collector batch (15s) + Prometheus scrape interval (usually 15s)
        await Task.Delay(TimeSpan.FromSeconds(20)); 

        // 2. Query Prometheus for the duration bucket metric filtered by the Materials service.
        var query = "http_server_request_duration_seconds_bucket{service_name=\"PoC-Materials\"}";
        
        try 
        {
            var result = await _observabilityClient.QueryPrometheusAsync(query);

            result.Should().NotBeNullOrEmpty("Prometheus query result should not be empty");
            result.Should().Contain("\"status\":\"success\"", "Response should indicate success");
            
            // Strong assertion: The actual metrics array `result[...]` must not be empty.
            // A naive string match for "PoC-Materials" could pass even if the result array is empty [] 
            // if the query string itself is echoed back.
            result.Should().NotContain("\"result\":[]", "Prometheus should return actual metric data points, not an empty array");
            result.Should().Contain("http.route", "The metric dimensions like route should be exported");
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail($"Prometheus Query failed to retrieve metrics for PoC-Materials: {ex.Message}");
        }
    }
}
