using Microsoft.Extensions.Configuration;
using RestSharp;
using System.Net;

namespace PoC.Observability.E2E;

public class LokiTests
{
    private readonly IConfiguration _configuration;
    private readonly ObservabilityClient _observabilityClient;
    private readonly RestClient _appClient;

    public LokiTests()
    {
        // Load configuration
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.test.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        _observabilityClient = new ObservabilityClient(_configuration);

        var appBaseUrl = _configuration["Observability:AppBaseUrl"] 
                         ?? throw new ArgumentNullException("Observability:AppBaseUrl");
        _appClient = new RestClient(appBaseUrl);
    }

    [Fact]
    public async Task Logs_Should_Be_Ingested_And_Queryable()
    {
        // 1. Generate logs by calling the API
        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);
        var response = await _appClient.ExecuteAsync(request);
        
        // Grab the Trace ID to uniquely identify this request's logs
        var traceIdHeader = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase));
        var traceId = traceIdHeader?.Value?.ToString();
        traceId.Should().NotBeNullOrWhiteSpace(because: "Trace ID should be returned to trace in Grafana");

        // 2. Wait a bit for ingestion (handled by retry policy in ObservabilityClient, but explicit delay helps)
        await Task.Delay(10000);

        // 3. Query Loki for logs specifically containing our Trace ID
        var query = $"{{job=\"PoC-Materials\"}} |= `{traceId}`";

        try 
        {
            var result = await _observabilityClient.QueryLokiAsync(query);
            
            // 4. Validate strong ingestion
            result.Should().NotBeNullOrEmpty("Loki query result should not be empty");
            result.Should().Contain("result", "Response should contain 'result' field");
            result.Should().NotContain("\"result\":[]", "Loki should return actual log lines correlated with the Trace ID, not an empty array");
            result.Should().Contain(traceId!, $"Loki should have ingested logs containing the Trace ID '{traceId}'");
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail($"Loki Query failed: {ex.Message}");
        }
    }

    [Fact]
    public async Task Error_Logs_Should_Be_Queryable()
    {
        // 1. Trigger an error (e.g., 404 or 400)
        var missingId = $"missing-{Guid.NewGuid():N}";
        var request = new RestRequest($"/api/v1/materials/{missingId}", Method.Get);
        var response = await _appClient.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        
        var traceIdHeader = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase));
        var traceId = traceIdHeader?.Value?.ToString();

        // 2. Wait for flush
        await Task.Delay(10000);
        
        // 3. Query for error logs correlated to this specific failing request
        var query = $"{{job=\"PoC-Materials\"}} |=\"{missingId}\" |=`{traceId}`";

        try 
        {
            var result = await _observabilityClient.QueryLokiAsync(query);
            
            result.Should().NotBeNullOrEmpty();
            result.Should().NotContain("\"result\":[]", "Loki must return the 404 error log linked to the missing ID");
            result.Should().Contain(missingId, "Log should contain the missing ID we requested.");
            result.Should().Contain(traceId!, "Log must be correctly correlated with the Trace ID of the failed request.");
        }
        catch (HttpRequestException ex)
        {
             Assert.Fail($"Loki Error Log Query failed: {ex.Message}");
        }
    }
}
