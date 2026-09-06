namespace OrderFlow.Orders.Infrastructure.Persistence.Entities;

public sealed class InboxMessageEntity
{
    public Guid EventId { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
}
