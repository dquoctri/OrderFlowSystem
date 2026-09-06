namespace OrderFlow.Orders.Infrastructure.Persistence.Entities;

/// <summary>Which saga step ended an order in <see cref="OrderStatus.Cancelled"/>.</summary>
public enum FailureStage
{
    ReservationRejected,
    PaymentDeclined
}
