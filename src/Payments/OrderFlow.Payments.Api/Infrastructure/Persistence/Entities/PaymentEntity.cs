namespace OrderFlow.Payments.Infrastructure.Persistence.Entities;

public sealed class PaymentEntity
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
