using System.Text.Json.Serialization;

namespace PoC.E2E.Tests;

public sealed record PopulationRequest(
    [property: JsonPropertyName("target")] string Target = "materials",
    [property: JsonPropertyName("count")] int Count = 0,
    [property: JsonPropertyName("min_components")] int? MinComponents = null,
    [property: JsonPropertyName("max_components")] int? MaxComponents = null
);
