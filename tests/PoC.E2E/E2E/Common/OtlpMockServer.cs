using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using WireMock.Settings;

namespace PoC.E2E.Common;

/// <summary>
/// A mock OTLP collector that captures trace and metric payloads using WireMock.Net.
/// Listens on a fixed port and stubs OTLP HTTP endpoints.
/// </summary>
public sealed class OtlpMockServer : IDisposable
{
    private readonly WireMockServer _server;

    public int Port { get; }

    public string Url => _server.Url!;

    public OtlpMockServer(int port = 14318)
    {
        Port = port;

        _server = WireMockServer.Start(new WireMockServerSettings
        {
            Port = port,
            ReadStaticMappings = false,
            Urls = new[] { $"http://+:{port}" }
        });

        _server
            .Given(Request.Create().WithPath("/v1/traces").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        _server
            .Given(Request.Create().WithPath("/v1/metrics").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));

        _server
            .Given(Request.Create().WithPath("/v1/logs").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200));
    }

    /// <summary>
    /// Returns the raw request bodies received at /v1/traces.
    /// </summary>
    /// <returns>A read-only list of byte arrays representing the trace payloads.</returns>
    public IReadOnlyList<byte[]> GetTracePayloads()
    {
        return _server.LogEntries
            .Where(e => e.RequestMessage.Path == "/v1/traces")
            .Select(e => e.RequestMessage.BodyAsBytes ?? Array.Empty<byte>())
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Returns the raw request bodies as strings for inspection.
    /// </summary>
    /// <returns>A read-only list of string payloads.</returns>
    public IReadOnlyList<string> GetTracePayloadsAsString()
    {
        return _server.LogEntries
            .Where(e => e.RequestMessage.Path == "/v1/traces")
            .Select(e => e.RequestMessage.Body ?? string.Empty)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Polls until at least one trace request is received, or timeout expires.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for trace payloads.</param>
    /// <param name="expectedCount">Minimum number of trace requests expected.</param>
    /// <returns>True if the expected count was reached before timeout.</returns>
    public async Task<bool> WaitForTraceAsync(TimeSpan timeout, int expectedCount = 1)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var count = _server.LogEntries
                .Count(e => e.RequestMessage.Path == "/v1/traces");

            if (count >= expectedCount)
            {
                return true;
            }

            await Task.Delay(250);
        }

        return false;
    }

    /// <summary>
    /// Resets all recorded log entries.
    /// </summary>
    public void Reset()
    {
        _server.ResetLogEntries();
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}
