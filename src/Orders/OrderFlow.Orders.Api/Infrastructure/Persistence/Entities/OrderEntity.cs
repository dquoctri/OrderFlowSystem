namespace OrderFlow.Orders.Infrastructure.Persistence.Entities;

public sealed class OrderEntity
{
    public Guid Id { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Set when the order reaches a terminal status (<c>Confirmed</c> or <c>Cancelled</c>).</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Which step cancelled the order; <c>null</c> unless <see cref="Status"/> is <c>Cancelled</c>.</summary>
    public FailureStage? FailureStage { get; set; }

    /// <summary>Human-readable reason carried by the failing event (<c>ReservationFailed</c> / <c>PaymentFailed</c>).</summary>
    public string? FailureReason { get; set; }

    public List<OrderLineEntity> Lines { get; set; } = new();
    public OrderSagaStateEntity SagaState { get; set; } = null!;
}
