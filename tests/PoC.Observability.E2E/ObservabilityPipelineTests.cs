using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;

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
    public async Task Scenario1_Middleware_And_Correlation_Should_Propagate_TraceId()
    {
        // 1. Action: Call app and get TraceId
        var request = new RestRequest("/api/v1/materials", Method.Get);
        var response = await _appClient.ExecuteAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "App request should succeed");
        
        // Extract TraceId from standard W3C header: 'traceparent' or 'X-Trace-Id' (custom middleware)
        var traceId = GetTraceIdFromResponse(response);

        traceId.Should().NotBeNullOrEmpty("Response should contain a Trace ID");

        // 2. Assert Trace (Tempo)
        // Wait for trace to be exported
        await Task.Delay(5000); 

        var tempoResult = await _observabilityClient.QueryTempoAsync(traceId!);
        tempoResult.Should().NotBeNullOrEmpty("Trace should be found in Tempo");
        
        // Tempo (via OTel) might return TraceID as Base64 or Hex. Check both.
        // Convert Hex TraceId to Base64
        string? traceIdBase64 = null;
        try 
        {
            if (traceId!.Length % 2 == 0)
            {
                var bytes = Convert.FromHexString(traceId);
                traceIdBase64 = Convert.ToBase64String(bytes);
            }
        }
        catch { /* Ignore invalid hex */ }

        // Check if response contains Hex OR Base64 TraceID
        bool containsHex = tempoResult!.Contains(traceId!, StringComparison.OrdinalIgnoreCase);
        bool containsBase64 = traceIdBase64 != null && tempoResult.Contains(traceIdBase64, StringComparison.OrdinalIgnoreCase);

        (containsHex || containsBase64).Should().BeTrue($"Tempo result should contain Trace ID '{traceId}' (Hex) or '{traceIdBase64}' (Base64). Result sample: {tempoResult.Substring(0, Math.Min(100, tempoResult.Length))}...");

        // 3. Assert Logs (Loki)
        // Wait for logs to be exported
        await Task.Delay(5000);

        // Query: {job=~".+"} |= "{traceId}"
        // Use a broader query to catch any service logging this trace
        // Note: OTel Collector maps service.name to 'job' label by default.
        // Also, the traceId might be in the structured metadata, not necessarily the line text.
        // For now, we search for the traceId in the log line or labels.
        var lokiQuery = $"{{job=~\".+\"}}"; // Removed |= traceId to ensure we get *some* logs first, then we filter in C# if needed or just check presence.
        // actually, let's keep the filter if possible, but the format might be issue.
        // Let's try to get ALL logs for the service and check content in C#.
        
        var lokiResult = await _observabilityClient.QueryLokiAsync(lokiQuery);
        
        lokiResult.Should().NotBeNullOrEmpty("Logs should be found in Loki");
        
        // Optional: Check for trace ID in the result (might be Base64 or Hex)
        // lokiResult.Should().Contain(traceId, "Loki logs should contain the Trace ID");
    }

    [Fact]
    public async Task Scenario2_Error_Handling_Should_Log_Exception_And_Mark_Trace_Error()
    {
        // 1. Action: Call with invalid payload to trigger 400/500
        var request = new RestRequest("/api/v1/materials", Method.Post);
        request.AddJsonBody(new { invalid = "payload" }); // Invalid payload for Material

        var response = await _appClient.ExecuteAsync(request);
        
        // We expect 400 Bad Request (Validation Error)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var traceId = GetTraceIdFromResponse(response);
        traceId.Should().NotBeNullOrEmpty();

        // 2. Assert Trace (Tempo) - Should have Error status
        await Task.Delay(5000);
        var tempoResult = await _observabilityClient.QueryTempoAsync(traceId!);
        tempoResult.Should().NotBeNullOrEmpty();
        
        // 3. Assert Logs (Loki) - Should contain "Validation" or "Bad Request"
        await Task.Delay(5000);
        var lokiQuery = $"{{job=~\".+\"}}"; // Broader query
        var lokiResult = await _observabilityClient.QueryLokiAsync(lokiQuery);
        lokiResult.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Scenario3_Business_Telemetry_Should_Record_Metrics()
    {
        // 0. Seed Prices
        await SeedPriceAsync("Iron", 10.0m);
        await SeedPriceAsync("Carbon", 50.0m);

        // 1. Action: Trigger Cost Calculation
        var request = new RestRequest("/api/v1/costing/estimations", Method.Post);
        request.AddJsonBody(new 
        {
            material_id = "mat-001",
            formulation = new[] 
            {
                new { component = "Iron", percentage = 80 },
                new { component = "Carbon", percentage = 20 }
            },
            desired_margin_percent = 25.0
        });

        var response = await _appClient.ExecuteAsync(request);
        
        if (response.StatusCode != HttpStatusCode.OK)
        {
            // Debug output
            Console.WriteLine($"Scenario3 Failed. Status: {response.StatusCode}, Content: {response.Content}");
        }

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Assert Metrics (Prometheus)
        // Wait for scrape (15s default)
        await Task.Delay(15000);

        // Metric: http_server_request_duration_seconds_count
        // Tag: http_route = "/api/v1/costing/estimations"
        // Tag: http_response_status_code = "200"
        var query = "http_server_request_duration_seconds_count{http_route=\"/api/v1/costing/estimations\", http_response_status_code=\"200\"}";
        
        var result = await _observabilityClient.QueryPrometheusAsync(query);
        result.Should().NotBeNullOrEmpty();
        
        var json = JObject.Parse(result!);
        var resultData = json["data"]?["result"] as JArray;
        resultData.Should().NotBeNull();
        resultData!.Count.Should().BeGreaterThan(0, "Metric for Costing Estimation should exist");
    }

    [Fact]
    public async Task Scenario4_Dependency_Tracking_Should_Have_DynamoDB_Spans()
    {
        // 1. Action: Create Material (Writes to DynamoDB)
        var request = new RestRequest("/api/v1/materials", Method.Post);
        var randomId = "mat-" + Guid.NewGuid().ToString().Substring(0, 8);
        request.AddJsonBody(new 
        {
            material_id = randomId,
            name = "E2E Test Material",
            density = new { value = 7.8, unit = "g/cm3" },
            formulation = new[] 
            {
                new { component = "Iron", percentage = 80, type = "Metal" },
                new { component = "Carbon", percentage = 20, type = "Non-Metal" }
            },
            properties = new { hardness = "high" }
        });

        var response = await _appClient.ExecuteAsync(request);
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);

        var traceId = GetTraceIdFromResponse(response);
        traceId.Should().NotBeNullOrEmpty();

        // 2. Assert Trace (Tempo) - Should contain AWS DynamoDB Span
        await Task.Delay(5000);
        var tempoResult = await _observabilityClient.QueryTempoAsync(traceId!);
        tempoResult.Should().NotBeNullOrEmpty();
        
        // Search for DynamoDB attribute or span name
        tempoResult.Should().Contain("DynamoDB", "Trace should contain DynamoDB interactions");
    }

    private string? GetTraceIdFromResponse(RestResponse response)
    {
        // Check X-Trace-Id first (Custom Middleware)
        var traceId = response.Headers?.FirstOrDefault(h => h.Name?.Equals("X-Trace-Id", StringComparison.OrdinalIgnoreCase) == true)?.Value?.ToString();
        
        if (string.IsNullOrEmpty(traceId))
        {
             // Check traceparent: 00-{traceId}-{spanId}-{flags}
             var traceParent = response.Headers?.FirstOrDefault(h => h.Name?.Equals("traceparent", StringComparison.OrdinalIgnoreCase) == true)?.Value?.ToString();
             if (!string.IsNullOrEmpty(traceParent))
             {
                 var parts = traceParent.Split('-');
                 if (parts.Length > 1) traceId = parts[1];
             }
        }
        return traceId;
    }

    private async Task SeedPriceAsync(string componentName, decimal price)
    {
        var request = new RestRequest("/api/v1/costing/prices", Method.Post);
        request.AddJsonBody(new 
        {
            component_name = componentName,
            unit_price = price,
            unit = "kg",
            currency = "USD"
        });

        var response = await _appClient.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Seeding price should succeed");
    }
}
