using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoC.Shared.Common;

/// <summary>
/// Provides centralized JSON serialization options to ensure consistency and performance across all services.
/// </summary>
public static class SerializationDefaults
{
    /// <summary>
    /// Standard JSON options for the entire application.
    /// Features:
    /// - Case-insensitive property matching (resilience)
    /// - CamelCase property naming policy (standard)
    /// - Enum conversion to string (readability)
    /// - Ignored null values (payload optimization)
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
