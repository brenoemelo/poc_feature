namespace PoC.Shared.Infrastructure.Configuration;

/// <summary>
/// Configuration options for OpenTelemetry and OTLP exporters.
/// </summary>
public sealed record OtelOptions
{
    /// <summary>
    /// Gets the OTLP endpoint URL. If null, a console exporter is used.
    /// </summary>
    public string? Endpoint { get; init; }

    /// <summary>
    /// Gets the OTLP protocol. Supported values: "grpc", "http". Default is "grpc".
    /// </summary>
    public string Protocol { get; init; } = "grpc";

    /// <summary>
    /// Gets a comma-separated list of headers (e.g., "Authorization=Basic xxx").
    /// </summary>
    public string? Headers { get; init; }

    /// <summary>
    /// Gets the deployment environment (e.g., "production", "staging").
    /// </summary>
    public string? Environment { get; init; }
}
