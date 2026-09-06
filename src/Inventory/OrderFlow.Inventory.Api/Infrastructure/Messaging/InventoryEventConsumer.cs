using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Infrastructure.Persistence;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;
using OrderFlow.Messaging;

namespace OrderFlow.Inventory.Infrastructure.Messaging;

public abstract class InventoryEventConsumer : PulsarConsumerWorker
{
    private readonly IServiceScopeFactory scopeFactory;

    protected InventoryEventConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger logger)
        : base(eventBus, logger) => this.scopeFactory = scopeFactory;

    protected override async Task HandleAsync(WireEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "orderflow_inventory"."inbox_messages" ("EventId", "ProcessedAt")
            VALUES ({message.EventId}, NOW())
            ON CONFLICT ("EventId") DO NOTHING
            """, cancellationToken);
        if (inserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        await ApplyAsync(db, message, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    protected abstract Task ApplyAsync(InventoryDbContext db, WireEvent message, CancellationToken cancellationToken);

    // Tracked on purpose: the row is re-read under a FOR UPDATE lock held for the whole
    // transaction, and EF change tracking then writes only the column the handler mutates.
    protected static async Task<StockItemEntity?> LockStockAsync(InventoryDbContext db, string sku, CancellationToken cancellationToken) =>
        await db.StockItems.FromSqlInterpolated($"""
            SELECT * FROM "orderflow_inventory"."stock_items"
            WHERE "Sku" = {sku} FOR UPDATE
            """).SingleOrDefaultAsync(cancellationToken);

    protected static void AddOutbox<T>(InventoryDbContext db, Guid orderId, string eventType, string topic, EventEnvelope<T> envelope) =>
        db.OutboxMessages.Add(new OutboxMessageEntity
        {
            EventId = envelope.EventId,
            OrderId = orderId,
            CorrelationId = envelope.CorrelationId,
            EventType = eventType,
            Topic = topic,
            Payload = JsonSerializer.Serialize(envelope),
            CreatedAt = envelope.Timestamp
        });
}
