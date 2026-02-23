using Microsoft.Extensions.Configuration;
using RestSharp;
using System.Net;

namespace PoC.Observability.E2E;

public class TempoTraceQLTests
{
    private readonly IConfiguration _configuration;
    private readonly ObservabilityClient _observabilityClient;
    private readonly RestClient _appClient;

    public TempoTraceQLTests()
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
    public async Task TraceQL_Complex_Query_Should_Succeed()
    {
        // 1. Action: Generate some traces by calling the API
        var request = new RestRequest("/api/v1/materials?limit=1", Method.Get);
        var response = await _appClient.ExecuteAsync(request);
        
        // Don't assert IsSuccessful here because Unleash delay might return 404, which still generates a trace.
        var traceIdHeader = response.Headers?.FirstOrDefault(h => string.Equals(h.Name, "X-Trace-Id", StringComparison.OrdinalIgnoreCase));
        var traceId = traceIdHeader?.Value?.ToString();
        traceId.Should().NotBeNullOrWhiteSpace(because: "Trace ID should be returned to trace in Grafana");

        // Wait for traces to be ingested by collector + tempo
        await Task.Delay(5000);

        // 2. Query Tempo directly for the specific Trace ID we just generated
        try 
        {
            var result = await _observabilityClient.QueryTempoAsync(traceId!);
            result.Should().NotBeNullOrEmpty("Tempo response should not be empty");
            result.Should().Contain(traceId, $"Result should contain the specific trace ID '{traceId}' reported by the API");
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail($"Tempo ID Lookup failed. The trace was not ingested: {ex.Message}");
        }
    }
}
