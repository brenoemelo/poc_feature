using PoC.Shared.Models;

namespace PoC.Shared.Events;

public class PriceUpdatedEvent : IEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    public string ComponentName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}
