using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Messaging;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public sealed class PaymentSucceededConsumer : OrdersEventConsumer
{
    public PaymentSucceededConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger<PaymentSucceededConsumer> logger)
        : base(eventBus, scopeFactory, logger) { }

    protected override string Topic => PulsarTopics.PaymentSucceeded;
    protected override string SubscriptionName => PulsarTopics.OrdersSubscription;
    protected override string EventSource => "payments";

    protected override void Apply(OrderEntity order, WireEvent message) =>
        OrderSagaTransitions.PaymentSucceeded(order);

    protected override string Detail(WireEvent message)
    {
        var paid = PulsarEventBus.DeserializeData<PaymentSucceeded>(message);
        return JsonSerializer.Serialize(new { amount = paid.Amount, paymentId = paid.PaymentId });
    }
}
