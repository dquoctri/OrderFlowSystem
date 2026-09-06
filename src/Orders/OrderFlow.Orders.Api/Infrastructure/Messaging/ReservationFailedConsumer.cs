using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Messaging;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public sealed class ReservationFailedConsumer : OrdersEventConsumer
{
    public ReservationFailedConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger<ReservationFailedConsumer> logger)
        : base(eventBus, scopeFactory, logger) { }

    protected override string Topic => PulsarTopics.ReservationFailed;
    protected override string SubscriptionName => PulsarTopics.OrdersSubscription;
    protected override string EventSource => "inventory";

    protected override void Apply(OrderEntity order, WireEvent message) =>
        OrderSagaTransitions.ReservationFailed(order, PulsarEventBus.DeserializeData<ReservationFailed>(message).Reason);

    protected override string Detail(WireEvent message) =>
        JsonSerializer.Serialize(new { reason = PulsarEventBus.DeserializeData<ReservationFailed>(message).Reason });
}
