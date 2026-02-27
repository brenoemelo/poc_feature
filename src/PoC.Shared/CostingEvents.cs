using System.Text.Json.Serialization;

namespace PoC.Shared.Events;

public sealed record PriceUpdatedEvent(
    [property: JsonPropertyName("component_name")] string ComponentName,
    [property: JsonPropertyName("unit_price")] decimal UnitPrice,
    [property: JsonPropertyName("unit")] string Unit,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt
) : IEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
