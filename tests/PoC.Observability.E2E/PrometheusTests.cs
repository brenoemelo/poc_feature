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
        // Arrange
        // Generate some traffic to ensure metrics are created
        var request = new RestRequest("/api/v1/materials", Method.Get);
        var response = await _appClient.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for metrics to be scraped (scrape interval is 10s/15s)
        await Task.Delay(15000); 

        // Act
        // Query for HTTP request duration histogram
        // OTel http.server.request.duration -> Prometheus http_server_request_duration_seconds_bucket
        var query = "http_server_request_duration_seconds_bucket";
        
        try 
        {
            var result = await _observabilityClient.QueryPrometheusAsync(query);

            // Assert
            result.Should().NotBeNullOrEmpty("Prometheus query result should not be empty");
            result.Should().Contain("\"status\":\"success\"", "Response should indicate success");
            result.Should().Contain("PoC-Materials", "Should contain metrics from PoC-Materials");
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail($"Prometheus Query failed: {ex.Message}");
        }
    }

    [Fact]
    public async Task Histograms_Should_Be_Queryable()
    {
        // Arrange
        // Generate traffic to ensure histogram data exists
        var request = new RestRequest("/api/v1/materials", Method.Get);
        await _appClient.ExecuteAsync(request);

        // Wait for metrics to be scraped
        await Task.Delay(15000);
        
        // Query for HTTP request duration histogram
        // This validates that histograms (native or classic) are being ingested
        var query = "http_server_request_duration_seconds_bucket";
        
        try 
        {
            var result = await _observabilityClient.QueryPrometheusAsync(query);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("PoC-Materials");
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail($"Prometheus Query failed: {ex.Message}");
        }
    }
}
