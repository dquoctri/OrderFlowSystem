using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Messaging;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public sealed class OrderPlacedConsumer : OrdersEventConsumer
{
    public OrderPlacedConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger<OrderPlacedConsumer> logger)
        : base(eventBus, scopeFactory, logger) { }

    protected override string Topic => PulsarTopics.OrderPlaced;
    protected override string SubscriptionName => PulsarTopics.OrdersStatusSubscription;
    protected override string EventSource => "orders";

    protected override void Apply(OrderEntity order, WireEvent message) =>
        OrderSagaTransitions.OrderPlaced(order);

    protected override string Detail(WireEvent message)
    {
        var placed = PulsarEventBus.DeserializeData<OrderPlaced>(message);
        return JsonSerializer.Serialize(new
        {
            customerId = placed.CustomerId,
            totalAmount = placed.TotalAmount,
            lineCount = placed.Lines.Count
        });
    }
}
