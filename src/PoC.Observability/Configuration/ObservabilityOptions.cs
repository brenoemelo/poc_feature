namespace PoC.Observability.Configuration;

/// <summary>
/// Configuration options for PoC Observability (OpenTelemetry).
/// </summary>
public class ObservabilityOptions
{
    /// <summary>
    /// Whether observability (telemetry) is enabled. Defaults to false.
    /// </summary>
    public bool Enabled { get; set; } = false;

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
    /// The OTLP Protocol (e.g., "grpc" or "http/protobuf").
    /// </summary>
    public string? OtlpProtocol { get; set; }

    /// <summary>
    /// The deployment environment (e.g., "Production", "Development").
    /// </summary>
    public string Environment { get; set; } = "Development";

    /// <summary>
    /// Whether to enable console logging (useful for local debugging).
    /// </summary>
    public bool ExportToConsole { get; set; } = true;
}
