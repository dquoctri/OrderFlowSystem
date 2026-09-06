using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Infrastructure.Persistence;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;
using OrderFlow.Messaging;

namespace OrderFlow.Inventory.Infrastructure.Messaging;

public class PaymentFailedConsumer : InventoryEventConsumer
{
    public PaymentFailedConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger<PaymentFailedConsumer> logger)
        : base(eventBus, scopeFactory, logger) { }

    protected override string Topic => PulsarTopics.PaymentFailed;
    protected override string SubscriptionName => PulsarTopics.InventorySubscription;

    protected override async Task ApplyAsync(InventoryDbContext db, WireEvent message, CancellationToken cancellationToken)
    {
        var reservations = await db.Reservations.Where(reservation => reservation.OrderId == message.OrderId && reservation.Status == ReservationStatus.Active)
            .OrderBy(reservation => reservation.Sku).ToListAsync(cancellationToken);
        var releasedLines = new List<OrderLineContract>();
        foreach (var reservation in reservations)
        {
            var stock = await LockStockAsync(db, reservation.Sku, cancellationToken)
                ?? throw new InvalidOperationException($"Stock item {reservation.Sku} does not exist.");
            StockOperations.Release(stock, reservation.Quantity);
            reservation.Status = ReservationStatus.Released;
            releasedLines.Add(new OrderLineContract(reservation.Sku, reservation.Quantity, 0));
        }

        var released = new EventEnvelope<StockReleased>(Guid.NewGuid(), message.CorrelationId, message.OrderId,
            DateTimeOffset.UtcNow, new StockReleased(releasedLines));
        AddOutbox(db, message.OrderId, nameof(StockReleased), PulsarTopics.StockReleased, released);
    }
}
