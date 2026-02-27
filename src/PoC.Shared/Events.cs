using PoC.Shared.Models;

namespace PoC.Shared.Events;

public interface IEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
}

public sealed record MaterialCreatedEvent(
    MaterialFormulation Material
) : IEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
