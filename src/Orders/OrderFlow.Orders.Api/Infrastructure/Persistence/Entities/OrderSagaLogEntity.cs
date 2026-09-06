namespace OrderFlow.Orders.Infrastructure.Persistence.Entities;

/// <summary>
/// Append-only record of one saga event as Orders observed it. Drives <c>GET /orders/{id}/trace</c>
/// and the UI event feed. One row per <c>(OrderId, EventType)</c> — idempotent.
/// </summary>
public sealed class OrderSagaLogEntity
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }

    /// <summary>1-based position of this event in the order's saga.</summary>
    public int Seq { get; set; }

    public string EventType { get; set; } = string.Empty;

    /// <summary>Service that published the event: <c>orders</c>, <c>inventory</c>, or <c>payments</c>.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>The event's own UTC timestamp.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>When Orders wrote this row.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    /// <summary>Small event-specific JSON payload (reason, amount, released lines…), or <c>null</c>.</summary>
    public string? Detail { get; set; }
}
