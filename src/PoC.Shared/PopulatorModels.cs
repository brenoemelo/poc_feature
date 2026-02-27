using System.Text.Json.Serialization;

namespace PoC.Shared.Models;

public sealed record PopulationRequest(
    [property: JsonPropertyName("target")] string Target = "materials",
    [property: JsonPropertyName("count")] int Count = 0,
    [property: JsonPropertyName("min_components")] int? MinComponents = null,
    [property: JsonPropertyName("max_components")] int? MaxComponents = null
);

public sealed record PopulationJobResponse(
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("total_records")] int TotalRecords,
    [property: JsonPropertyName("batches_queued")] int BatchesQueued
);
