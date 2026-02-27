using System.Text.Json.Serialization;

namespace PoC.Populator.Domain.Models;

public sealed record PopulationJob(
    [property: JsonPropertyName("target")] string Target,
    [property: JsonPropertyName("batch_size")] int BatchSize,
    [property: JsonPropertyName("min_components")] int? MinComponents,
    [property: JsonPropertyName("max_components")] int? MaxComponents
);
