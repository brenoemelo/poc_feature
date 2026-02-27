namespace PoC.Observability.Configuration;

public class ObservabilityOptions
{
    public bool Enabled { get; set; }

    public required string ServiceName { get; set; }

    public string ServiceVersion { get; set; } = "1.0.0";

    public string? OtlpEndpoint { get; set; }

    public string? OtlpProtocol { get; set; }

    public string Environment { get; set; } = "Development";

    public bool ExportToConsole { get; set; } = true;

    public void Validate()
    {
        if (Enabled && !string.IsNullOrEmpty(OtlpEndpoint) && !Uri.TryCreate(OtlpEndpoint, UriKind.Absolute, out _))
            throw new InvalidOperationException($"Invalid OTLP endpoint URI: '{OtlpEndpoint}'");
    }
}
