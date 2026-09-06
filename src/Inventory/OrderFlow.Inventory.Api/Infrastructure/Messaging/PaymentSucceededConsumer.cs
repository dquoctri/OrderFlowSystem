using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Inventory.Infrastructure.Persistence;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;
using OrderFlow.Messaging;

namespace OrderFlow.Inventory.Infrastructure.Messaging;

public sealed class PaymentSucceededConsumer : InventoryEventConsumer
{
    public PaymentSucceededConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger<PaymentSucceededConsumer> logger)
        : base(eventBus, scopeFactory, logger) { }

    protected override string Topic => PulsarTopics.PaymentSucceeded;
    protected override string SubscriptionName => PulsarTopics.InventorySubscription;

    protected override async Task ApplyAsync(InventoryDbContext db, WireEvent message, CancellationToken cancellationToken)
    {
        var reservations = await db.Reservations.Where(reservation => reservation.OrderId == message.OrderId && reservation.Status == ReservationStatus.Active)
            .OrderBy(reservation => reservation.Sku).ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            var stock = await LockStockAsync(db, reservation.Sku, cancellationToken)
                ?? throw new InvalidOperationException($"Stock item {reservation.Sku} does not exist.");
            StockOperations.Consume(stock, reservation.Quantity);
            reservation.Status = ReservationStatus.Consumed;
        }
    }
}
