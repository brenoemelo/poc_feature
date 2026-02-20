using FluentAssertions;
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
        var request = new RestRequest("/api/v1/materials", Method.Get);
        var response = await _appClient.ExecuteAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for traces to be ingested
        await Task.Delay(5000);

        // 2. Execute Complex TraceQL Query
        // This query uses 'select' which requires vParquet4 and compatible Tempo version.
        // Note: 'order_by' is not yet supported in current TraceQL implementation.
        var query = "{ span.duration > 0ms } | select(.duration, .status)";
        
        try 
        {
            var result = await _observabilityClient.QueryTempoSearchAsync(query);
            result.Should().NotBeNullOrEmpty("Tempo search result should not be empty");
            result.Should().Contain("traces", "Result should contain 'traces' array");
            // If vParquet4 is working, we might see specific structure, but 200 OK is the main goal here.
        }
        catch (HttpRequestException ex)
        {
            // If it fails with 400, it means the feature is not supported or config is wrong
            Assert.Fail($"Tempo Search failed: {ex.Message}");
        }
    }
}
