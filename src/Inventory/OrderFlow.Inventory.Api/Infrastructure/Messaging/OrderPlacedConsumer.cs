using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Infrastructure.Persistence;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;
using OrderFlow.Messaging;

namespace OrderFlow.Inventory.Infrastructure.Messaging;

public class OrderPlacedConsumer : InventoryEventConsumer
{
    public OrderPlacedConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger<OrderPlacedConsumer> logger)
        : base(eventBus, scopeFactory, logger) { }

    protected override string Topic => PulsarTopics.OrderPlaced;
    protected override string SubscriptionName => PulsarTopics.InventorySubscription;

    protected override async Task ApplyAsync(InventoryDbContext db, WireEvent message, CancellationToken cancellationToken)
    {
        var order = PulsarEventBus.DeserializeData<OrderPlaced>(message);
        var lines = OrderReservation.Consolidate(order.Lines);

        var locked = new Dictionary<string, StockItemEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            if (await LockStockAsync(db, line.Sku, cancellationToken) is { } stock)
            {
                locked[line.Sku] = stock;
            }
        }

        if (OrderReservation.Shortfall(lines, sku => locked.GetValueOrDefault(sku)) is { } reason)
        {
            var failure = new EventEnvelope<ReservationFailed>(Guid.NewGuid(), message.CorrelationId, message.OrderId,
                DateTimeOffset.UtcNow, new ReservationFailed(reason));
            AddOutbox(db, message.OrderId, nameof(ReservationFailed), PulsarTopics.ReservationFailed, failure);
            return;
        }

        foreach (var line in lines)
        {
            StockOperations.Reserve(locked[line.Sku], line.Quantity);
            db.Reservations.Add(new ReservationEntity
            {
                OrderId = message.OrderId,
                Sku = line.Sku,
                Quantity = line.Quantity,
                Status = ReservationStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        var success = new EventEnvelope<ReservationSucceeded>(Guid.NewGuid(), message.CorrelationId, message.OrderId,
            DateTimeOffset.UtcNow, new ReservationSucceeded(Guid.NewGuid(), lines, order.TotalAmount));
        AddOutbox(db, message.OrderId, nameof(ReservationSucceeded), PulsarTopics.ReservationSucceeded, success);
    }
}
