using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Messaging;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public sealed class ReservationSucceededConsumer : OrdersEventConsumer
{
    public ReservationSucceededConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger<ReservationSucceededConsumer> logger)
        : base(eventBus, scopeFactory, logger) { }

    protected override string Topic => PulsarTopics.ReservationSucceeded;
    protected override string SubscriptionName => PulsarTopics.OrdersSubscription;
    protected override string EventSource => "inventory";

    protected override void Apply(OrderEntity order, WireEvent message) =>
        OrderSagaTransitions.ReservationSucceeded(order);

    protected override string Detail(WireEvent message)
    {
        var reserved = PulsarEventBus.DeserializeData<ReservationSucceeded>(message);
        return JsonSerializer.Serialize(new { amount = reserved.Amount, lineCount = reserved.Lines.Count });
    }
}
