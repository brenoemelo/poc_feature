using System.Text.Json.Serialization;
using PoC.Shared.Models;

namespace PoC.Shared.Events;

public interface IEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
}

public sealed record MaterialCreatedEvent(
    [property: JsonPropertyName("material")] MaterialFormulation Material
) : IEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
