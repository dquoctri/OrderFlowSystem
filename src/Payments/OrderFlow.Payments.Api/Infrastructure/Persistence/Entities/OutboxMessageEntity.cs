namespace OrderFlow.Payments.Infrastructure.Persistence.Entities;

public sealed class OutboxMessageEntity
{
    public long Id { get; set; }
    public Guid EventId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CorrelationId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? LockOwner { get; set; }
    public DateTimeOffset? LockExpiresAt { get; set; }
    public int Attempts { get; set; }
}
