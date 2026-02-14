using System.Text.Json.Serialization;

using PoC.Shared.Common;

namespace PoC.Shared.Models;

public sealed class MaterialFormulation : BaseEntity
{
    [JsonPropertyName("material_id")]
    public string MaterialId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("density")]
    public Density? Density { get; set; }

    [JsonPropertyName("formulation")]
    public List<FormulationComponent> Formulation { get; set; } = new();

    [JsonPropertyName("properties")]
    public Dictionary<string, string> Properties { get; set; } = new();
}

public class Density
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;
}

public class FormulationComponent
{
    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;

    [JsonPropertyName("percentage")]
    public double Percentage { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}
