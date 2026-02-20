using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using RestSharp;

namespace PoC.Observability.E2E;

public class ObservabilityClient
{
    private readonly RestClient _prometheusClient;
    private readonly RestClient _tempoClient;
    private readonly RestClient _lokiClient;
    private readonly RestClient _collectorHealthClient;
    private readonly AsyncRetryPolicy<RestResponse> _retryPolicy;

    public ObservabilityClient(IConfiguration configuration)
    {
        var prometheusUrl = configuration["Observability:PrometheusUrl"] 
                            ?? throw new ArgumentNullException("Observability:PrometheusUrl");
        var tempoUrl = configuration["Observability:TempoUrl"] 
                       ?? throw new ArgumentNullException("Observability:TempoUrl");
        var lokiUrl = configuration["Observability:LokiUrl"] 
                      ?? throw new ArgumentNullException("Observability:LokiUrl");
        var collectorHealthUrl = configuration["Observability:CollectorHealthUrl"]
                                 ?? "http://localhost:13133";

        _prometheusClient = new RestClient(prometheusUrl);
        _tempoClient = new RestClient(tempoUrl);
        _lokiClient = new RestClient(lokiUrl);
        _collectorHealthClient = new RestClient(collectorHealthUrl);

        // Increased retry for eventual consistency (especially Loki which can be slow)
        _retryPolicy = Policy
            .Handle<Exception>()
            // We retry on NotFound as well because in eventual consistency (Tempo/Loki), 
            // the resource might not be available yet.
            .OrResult<RestResponse>(r => !r.IsSuccessful) 
            .WaitAndRetryAsync(30, retryAttempt => TimeSpan.FromSeconds(2), (outcome, timeSpan, retryCount, context) =>
            {
                // Console.WriteLine($"Retry {retryCount} due to {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
            });
    }

    public async Task<bool> CheckCollectorHealthAsync()
    {
        var request = new RestRequest("/health", Method.Get);
        var response = await _collectorHealthClient.ExecuteAsync(request);
        return response.IsSuccessful;
    }

    public async Task<string?> QueryPrometheusAsync(string query)
    {
        var request = new RestRequest("/api/v1/query", Method.Get);
        request.AddQueryParameter("query", query);

        // Retry if result array is empty
        return await ExecuteWithRetryAsync(_prometheusClient, request, r => string.IsNullOrEmpty(r.Content) || r.Content.Contains("\"result\":[]"));
    }

    public async Task<string?> QueryTempoAsync(string traceId)
    {
        var request = new RestRequest($"/api/traces/{traceId}", Method.Get);

        return await ExecuteWithRetryAsync(_tempoClient, request);
    }

    public async Task<string?> QueryTempoSearchAsync(string traceqlQuery)
    {
        var request = new RestRequest("/api/search", Method.Get);
        request.AddQueryParameter("q", traceqlQuery);

        return await ExecuteWithRetryAsync(_tempoClient, request);
    }

    public async Task<string?> QueryLokiAsync(string query)
    {
        var request = new RestRequest("/loki/api/v1/query_range", Method.Get);
        request.AddQueryParameter("query", query);
        request.AddQueryParameter("limit", "10");

        // Retry if result array is empty (Loki query_range returns "result":[])
        return await ExecuteWithRetryAsync(_lokiClient, request, r => string.IsNullOrEmpty(r.Content) || r.Content.Contains("\"result\":[]"));
    }

    private async Task<string?> ExecuteWithRetryAsync(RestClient client, RestRequest request, Func<RestResponse, bool>? shouldRetryData = null)
    {
        var result = await _retryPolicy.ExecuteAsync(async () =>
        {
            var response = await client.ExecuteAsync(request);
            
            if (!response.IsSuccessful)
            {
                throw new HttpRequestException($"Request failed with status {response.StatusCode}: {response.Content}");
            }

            if (shouldRetryData != null && shouldRetryData(response))
            {
                throw new HttpRequestException("Data not yet available in the backend.");
            }

            return response;
        });

        return result.Content;
    }
}
