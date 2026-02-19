
namespace PoC.Observability.Configuration;

/// <summary>
/// Configuration options for PoC Observability (OpenTelemetry).
/// </summary>
public class ObservabilityOptions
{
    /// <summary>
    /// The name of the service (e.g., "PoC-Costing").
    /// </summary>
    public required string ServiceName { get; set; }

    /// <summary>
    /// The application version.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// The OTLP endpoint (e.g., "http://otel-collector:4318").
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Whether to enable console logging (useful for local debugging).
    /// </summary>
    public bool EnableConsoleLogging { get; set; } = true;
}
