using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Messaging;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure.Messaging;

/// <summary>
/// Records the compensation step in the saga trace. It does not change the order status —
/// <see cref="PaymentFailedConsumer"/> already moved the order to <c>Cancelled</c>.
/// </summary>
public sealed class StockReleasedConsumer : OrdersEventConsumer
{
    public StockReleasedConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger<StockReleasedConsumer> logger)
        : base(eventBus, scopeFactory, logger) { }

    protected override string Topic => PulsarTopics.StockReleased;
    protected override string SubscriptionName => PulsarTopics.OrdersSubscription;
    protected override string EventSource => "inventory";

    protected override void Apply(OrderEntity order, WireEvent message)
    {
    }

    protected override string Detail(WireEvent message)
    {
        var released = PulsarEventBus.DeserializeData<StockReleased>(message);
        return JsonSerializer.Serialize(new
        {
            releasedLines = released.ReleasedLines.Select(line => new { sku = line.Sku, quantity = line.Quantity })
        });
    }
}
