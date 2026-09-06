namespace OrderFlow.Messaging;

public static class PulsarTopics
{
    public const string OrderPlaced = "persistent://public/default/orderflow.order-placed";
    public const string ReservationSucceeded = "persistent://public/default/orderflow.reservation-succeeded";
    public const string ReservationFailed = "persistent://public/default/orderflow.reservation-failed";
    public const string PaymentSucceeded = "persistent://public/default/orderflow.payment-succeeded";
    public const string PaymentFailed = "persistent://public/default/orderflow.payment-failed";
    public const string StockReleased = "persistent://public/default/orderflow.stock-released";

    public const string OrdersSubscription = "orderflow.orders.saga.v1";
    public const string OrdersStatusSubscription = "orderflow.orders.status.v1";
    public const string InventorySubscription = "orderflow.inventory.saga.v1";
    public const string PaymentsSubscription = "orderflow.payments.saga.v1";

    public static IReadOnlyList<string> ApplicationTopics { get; } = new[]
    {
        OrderPlaced,
        ReservationSucceeded,
        ReservationFailed,
        PaymentSucceeded,
        PaymentFailed,
        StockReleased
    };

    public static string? Resolve(string? topic) => topic?.ToLowerInvariant() switch
    {
        "order-placed" => OrderPlaced,
        "reservation-succeeded" => ReservationSucceeded,
        "reservation-failed" => ReservationFailed,
        "payment-succeeded" => PaymentSucceeded,
        "payment-failed" => PaymentFailed,
        "stock-released" => StockReleased,
        _ when topic is not null && ApplicationTopics.Contains(topic) => topic,
        _ => null
    };

    public static string DeadLetter(string topic) => $"{topic}.dlq";
}
