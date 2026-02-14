using System.Text.Json.Serialization;
using PoC.Shared.Models;

namespace PoC.Shared.Events;

public interface IEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
}

public class MaterialCreatedEvent : IEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("material")]
    public MaterialFormulation Material { get; set; } = new();
}
