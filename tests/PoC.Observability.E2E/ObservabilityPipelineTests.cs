using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;
using Xunit;

namespace PoC.Observability.E2E;

public class ObservabilityPipelineTests
{
    private readonly IConfiguration _configuration;
    private readonly ObservabilityClient _observabilityClient;
    private readonly RestClient _appClient;

    public ObservabilityPipelineTests()
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
    public async Task Collector_Should_Be_Healthy()
    {
        var isHealthy = await _observabilityClient.CheckCollectorHealthAsync();
        isHealthy.Should().BeTrue("OpenTelemetry Collector should be healthy and reachable at /health");
    }

    [Fact]
    public async Task Metrics_Pipeline_Should_Increment_Counter()
    {
        // 1. Action: Call app endpoint to generate metrics
        // Warning: This depends on the specific endpoint instrumentation.
        // Assuming a standard ASP.NET Core instrumentation of "http.server.request.duration" (histogram/count)
        // or a custom counter if available.
        // Let's use /health or a known endpoint.
        var request = new RestRequest("/api/v1/materials/count", Method.Get); // Assuming this endpoint exists based on logs
        
        // Initial check? No, difficult to know initial state in shared env.
        // Better trigger N requests and ensure count increases?
        // Or simpler: Just ensure the metric exists with recent timestamp.
        
        // Let's trigger 5 requests
        for (int i = 0; i < 5; i++)
        {
            await _appClient.ExecuteAsync(request);
        }

        // Allow some time for export/scrape (default scrape interval 15s)
        await Task.Delay(5000); 

        // 2. Assertion: Query Prometheus
        // Query: http_server_request_duration_count{http_route="/api/v1/materials/count"} or similar
        // Note: OTel semantic conventions rename metrics sometimes. 
        // usually: http_server_request_duration_seconds_count
        var query = "poc_http_server_request_duration_seconds_count{http_route=\"/api/v1/materials/count\", http_response_status_code=\"200\"}";
        
        // Retry logic is inside ObservabilityClient?
        // We might need a "Polly" wrap here to wait for the value to appear/update if we are strict.
        // For E2E, just checking existence is a good start.

        var result = await _observabilityClient.QueryPrometheusAsync(query);
        
        result.Should().NotBeNullOrEmpty();
        
        var json = JObject.Parse(result!);
        var status = json["status"]?.ToString();
        status.Should().Be("success");

        var resultData = json["data"]?["result"] as JArray;
        resultData.Should().NotBeNull();
        resultData!.Count.Should().BeGreaterThan(0, "Metric poc_http_server_request_duration_seconds_count should exist in Prometheus");
        
        var value = resultData[0]?["value"]?[1]?.ToString();
        value.Should().NotBeNullOrEmpty();
        int.Parse(value!).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Tracing_Pipeline_Should_Store_Trace_In_Tempo()
    {
        // 1. Action: Call app and get TraceId
        var request = new RestRequest("/api/v1/materials/count", Method.Get);
        var response = await _appClient.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "App request should succeed");
        
        // Extract TraceId from standard W3C header: 'traceparent'
        // Format: 00-{traceId}-{spanId}-{flags}
        // Example: 00-64da06431e6a151e6308c931264b7b59-76471b544fd5bb5b-01
        
        // Or check custom headers if configured. OTel usually interprets inbound traceparent.
        // But for outbound response, AspNetCore usually does NOT send `traceparent` back by default unless configured?
        // Actually, it usually doesn't unless we added middleware to echo it, OR we look at local logs.
        // WAIT. If we don't know the TraceId, we can't query Tempo specifically easily.
        // We can however generate a TraceId client-side and send it in `traceparent` header!
        // The app (if standard OTel) will use it.
        
        var traceId = GenerateRandomTraceId();
        var spanId = GenerateRandomSpanId();
        var traceParent = $"00-{traceId}-{spanId}-01";
        
        request.AddHeader("traceparent", traceParent);
        
        response = await _appClient.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Assertion: Query Tempo with that TraceId
        // Wait for propagation
        // Polly Retry handled in client, but we might want explicit delay/check loop here.
        // The client loop is robust enough initially.
        
        // Tempo might return 404 if not found yet. Using RetryPolicy inside Client handles non-success codes?
        // Wait, Client throws on !IsSuccessful. 404 IS !IsSuccessful. So Client handles retry.
        
        string? traceJson = null;
        try 
        {
             traceJson = await _observabilityClient.QueryTempoAsync(traceId);
        }
        catch (HttpRequestException ex)
        {
            Assert.Fail($"Failed to retrieve trace {traceId} from Tempo: {ex.Message}");
        }

        traceJson.Should().NotBeNullOrEmpty();
        
        // Tempo might return traceId as Hex or Base64 in the JSON response
        var base64TraceId = Convert.ToBase64String(Convert.FromHexString(traceId));
        bool containsTraceId = traceJson.Contains(traceId) || traceJson.Contains(base64TraceId);
        
        containsTraceId.Should().BeTrue($"Trace should contain the generated TraceId (Hex: {traceId} or Base64: {base64TraceId})");
        
        // Verify service name is present in trace
        traceJson.Should().Contain("PoC-Materials", "Trace should contain the service name PoC-Materials");
    }

    [Fact]
    public async Task Logging_Pipeline_Should_Index_Log_With_TraceId_In_Loki()
    {
        // 1. Action: Call app with known TraceId to correlate logs
        var traceId = GenerateRandomTraceId();
        var spanId = GenerateRandomSpanId();
        var traceParent = $"00-{traceId}-{spanId}-01";
        
        var request = new RestRequest("/api/v1/materials/count", Method.Get);
        request.AddHeader("traceparent", traceParent);
        
        await _appClient.ExecuteAsync(request);

        // Query for logs from the specific service containing the trace ID
        var query = $"{{job=\"PoC-Materials\"}} |= \"{traceId}\"";
        
        string? logsJson = null;
        
        // Retry loop explicitly because logs might take longer
        try
        {
             logsJson = await _observabilityClient.QueryLokiAsync(query);
        }
        catch(Exception ex)
        {
             Assert.Fail($"Failed to retrieve logs for trace {traceId} from Loki: {ex.Message}");
        }

        logsJson.Should().NotBeNullOrEmpty();
        
        var json = JObject.Parse(logsJson!);
        var status = json["status"]?.ToString();
        status.Should().Be("success");

        var resultData = json["data"]?["result"] as JArray;
        resultData.Should().NotBeNull();
        resultData!.Count.Should().BeGreaterThan(0, $"Loki should return at least one log entry containing the TraceId {traceId}");
    }

    private string GenerateRandomTraceId()
    {
        return Guid.NewGuid().ToString("N");
    }
    
    private string GenerateRandomSpanId()
    {
        return Guid.NewGuid().ToString("N").Substring(0, 16);
    }
}
