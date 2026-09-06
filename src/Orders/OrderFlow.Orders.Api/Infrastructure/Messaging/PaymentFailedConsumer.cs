using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Messaging;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public sealed class PaymentFailedConsumer : OrdersEventConsumer
{
    public PaymentFailedConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger<PaymentFailedConsumer> logger)
        : base(eventBus, scopeFactory, logger) { }

    protected override string Topic => PulsarTopics.PaymentFailed;
    protected override string SubscriptionName => PulsarTopics.OrdersSubscription;
    protected override string EventSource => "payments";

    protected override void Apply(OrderEntity order, WireEvent message) =>
        OrderSagaTransitions.PaymentFailed(order, PulsarEventBus.DeserializeData<PaymentFailed>(message).Reason);

    protected override string Detail(WireEvent message) =>
        JsonSerializer.Serialize(new { reason = PulsarEventBus.DeserializeData<PaymentFailed>(message).Reason });
}
