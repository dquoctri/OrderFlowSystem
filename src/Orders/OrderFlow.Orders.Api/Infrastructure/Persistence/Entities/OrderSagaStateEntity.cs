namespace OrderFlow.Orders.Infrastructure.Persistence.Entities;

public sealed class OrderSagaStateEntity
{
    public Guid OrderId { get; set; }
    public bool ReservationCompleted { get; set; }
    public bool PaymentCompleted { get; set; }
    public Guid? LastProcessedEventId { get; set; }
    public OrderEntity Order { get; set; } = null!;
}
