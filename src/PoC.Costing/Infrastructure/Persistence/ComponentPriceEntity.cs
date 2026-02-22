using System.Text.Json.Serialization;

namespace PoC.Costing.Infrastructure.Persistence;

public class ComponentPriceEntity
{
    [JsonPropertyName("ComponentName")]
    public string ComponentName { get; set; } = string.Empty;

    [JsonPropertyName("UnitPrice")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("Unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("Currency")]
    public string Currency { get; set; } = string.Empty;

    [JsonPropertyName("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("Version")]
    public int? Version { get; set; }
}
