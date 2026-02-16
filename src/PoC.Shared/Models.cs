using System.Text.Json.Serialization;

namespace PoC.Shared.Models;

public sealed record MaterialFormulation(
    [property: JsonPropertyName("material_id")] string MaterialId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("density")] Density? Density,
    [property: JsonPropertyName("formulation")] List<FormulationComponent> Formulation,
    [property: JsonPropertyName("properties")] Dictionary<string, string> Properties,
    [property: JsonPropertyName("version")] int? Version
);

public sealed record Density(
    [property: JsonPropertyName("value")] double Value,
    [property: JsonPropertyName("unit")] string Unit
);

public sealed record FormulationComponent(
    [property: JsonPropertyName("component")] string Component,
    [property: JsonPropertyName("percentage")] double Percentage,
    [property: JsonPropertyName("type")] string Type
);
