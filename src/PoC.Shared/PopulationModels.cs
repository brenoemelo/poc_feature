using System.Text.Json.Serialization;

namespace PoC.Shared.Models;

public sealed record PopulationRequest(
    [property: JsonPropertyName("target")] string Target = "materials",
    [property: JsonPropertyName("count")] int Count = 0,
    [property: JsonPropertyName("min_components")] int? MinComponents = null,
    [property: JsonPropertyName("max_components")] int? MaxComponents = null
);

public sealed record PopulationJob(
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("batch_size")] int BatchSize,
    [property: JsonPropertyName("min_components")] int? MinComponents,
    [property: JsonPropertyName("max_components")] int? MaxComponents
);
