using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure;

/// <summary>
/// The order status-transition rules for each saga event, in one place. Every transition is
/// guarded so a late or duplicate event can never move an order out of a terminal status.
/// </summary>
public static class OrderSagaTransitions
{
    private static bool IsTerminal(OrderStatus status) =>
        status is OrderStatus.Confirmed or OrderStatus.Cancelled;

    public static void OrderPlaced(OrderEntity order)
    {
        if (order.Status is OrderStatus.Pending)
        {
            order.Status = OrderStatus.Reserving;
        }
    }

    public static void ReservationSucceeded(OrderEntity order)
    {
        order.SagaState.ReservationCompleted = true;
        if (!IsTerminal(order.Status))
        {
            order.Status = OrderStatus.Charging;
        }
    }

    public static void ReservationFailed(OrderEntity order, string reason)
    {
        if (order.Status is OrderStatus.Confirmed)
        {
            return;
        }

        order.Status = OrderStatus.Cancelled;
        order.FailureStage ??= FailureStage.ReservationRejected;
        order.FailureReason ??= reason;
        MarkCompleted(order);
    }

    public static void PaymentSucceeded(OrderEntity order)
    {
        order.SagaState.PaymentCompleted = true;
        if (order.Status is not OrderStatus.Cancelled)
        {
            order.Status = OrderStatus.Confirmed;
            MarkCompleted(order);
        }
    }

    public static void PaymentFailed(OrderEntity order, string reason)
    {
        if (order.Status is OrderStatus.Confirmed)
        {
            return;
        }

        order.Status = OrderStatus.Cancelled;
        order.FailureStage ??= FailureStage.PaymentDeclined;
        order.FailureReason ??= reason;
        MarkCompleted(order);
    }

    private static void MarkCompleted(OrderEntity order) =>
        order.CompletedAt ??= DateTimeOffset.UtcNow;
}
