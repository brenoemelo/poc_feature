using System.Text.Json.Serialization;

namespace PoC.Shared.Http;

/// <summary>
/// A machine-readable format for specifying errors in HTTP API responses based on https://tools.ietf.org/html/rfc7807.
/// </summary>
public class ProblemDetails
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("status")]
    public int? Status { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("instance")]
    public string? Instance { get; set; }

    [JsonExtensionData]
    public IDictionary<string, object?> Extensions { get; set; } = new Dictionary<string, object?>(StringComparer.Ordinal);
}

public class ValidationProblemDetails : ProblemDetails
{
    public ValidationProblemDetails()
    {
        Title = "One or more validation errors occurred.";
        Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
        Status = 400;
    }

    [JsonPropertyName("errors")]
    public IDictionary<string, string[]> Errors { get; set; } = new Dictionary<string, string[]>(StringComparer.Ordinal);
}
