using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Messaging;
using OrderFlow.Orders.Infrastructure.Persistence;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public abstract class OrdersEventConsumer : PulsarConsumerWorker
{
    private readonly IServiceScopeFactory scopeFactory;

    protected OrdersEventConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, ILogger logger)
        : base(eventBus, logger) => this.scopeFactory = scopeFactory;

    /// <summary>Service that publishes the event this consumer handles: <c>orders</c> / <c>inventory</c> / <c>payments</c>.</summary>
    protected abstract string EventSource { get; }

    protected override async Task HandleAsync(WireEvent message, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "orderflow_orders"."inbox_messages" ("EventId", "ProcessedAt")
            VALUES ({message.EventId}, NOW())
            ON CONFLICT ("EventId") DO NOTHING
            """, cancellationToken);
        if (inserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        // Different topic consumers can run concurrently even with KeyShared subscriptions.
        // Serialize the per-order log cursor allocation so SSE resume IDs cannot collide.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT 1 FROM "orderflow_orders"."orders" WHERE "Id" = {message.OrderId} FOR UPDATE
            """, cancellationToken);

        var order = await db.Orders.Include(value => value.SagaState)
            .SingleOrDefaultAsync(value => value.Id == message.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {message.OrderId} does not exist.");

        Apply(order, message);
        order.SagaState.LastProcessedEventId = message.EventId;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await AppendSagaLogAsync(db, order.Id, message, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    protected abstract void Apply(OrderEntity order, WireEvent message);

    /// <summary>Optional small JSON payload stored with the saga-log row for this event.</summary>
    protected virtual string? Detail(WireEvent message) => null;

    private async Task AppendSagaLogAsync(OrdersDbContext db, Guid orderId, WireEvent message, CancellationToken cancellationToken)
    {
        var nextSeq = 1 + await db.OrderSagaLog.CountAsync(log => log.OrderId == orderId, cancellationToken);
        db.OrderSagaLog.Add(new OrderSagaLogEntity
        {
            OrderId = orderId,
            Seq = nextSeq,
            EventType = message.EventType,
            Source = EventSource,
            OccurredAt = message.Timestamp,
            RecordedAt = DateTimeOffset.UtcNow,
            Detail = Detail(message)
        });
    }
}
