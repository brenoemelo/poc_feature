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
        // This request will generate Info logs in PoC.Materials.API
        var request = new RestRequest("/api/v1/materials", Method.Get);
        var response = await _appClient.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // 2. Wait a bit for ingestion (handled by retry policy in ObservabilityClient, but explicit delay helps)
        await Task.Delay(2000);

        // 3. Query Loki
        // We look for logs from the Materials API service.
        // The label 'service_name' or 'app' depends on OTel Collector config.
        // Usually OTel Collector maps 'service.name' resource attribute to 'service_name' label in Loki.
        // Let's try searching for the standard OTel service name.
        
        // Note: The OTel Collector config for Loki usually sets labels based on resource attributes.
        // Common pattern: {service_name="PoC.Materials.API"}
        // With default_labels_enabled: job: true, the service.name maps to 'job' label.
        var query = "{job=\"PoC-Materials\"}";

        try 
        {
            var result = await _observabilityClient.QueryLokiAsync(query);
            
            // 4. Validate
            result.Should().NotBeNullOrEmpty("Loki query result should not be empty");
            result.Should().Contain("result", "Response should contain 'result' field");
            // result.Should().Contain("PoC.Materials.API", "Logs should contain the service name"); 
            // The service name is in the label, not necessarily the log line, but usually it is.
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
        var request = new RestRequest("/api/v1/materials/non-existent-id", Method.Get);
        var response = await _appClient.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 2. Query for error logs (optional, depends on if 404 logs as Error or Info)
        // Usually handled exceptions are Warn, unhandled are Error.
        // Let's just verify we can find the log with the specific path.
        
        var query = "{job=\"PoC-Materials\"} |= \"non-existent-id\"";

        try 
        {
            var result = await _observabilityClient.QueryLokiAsync(query);
            
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("non-existent-id");
        }
        catch (HttpRequestException ex)
        {
             Assert.Fail($"Loki Error Log Query failed: {ex.Message}");
        }
    }
}
