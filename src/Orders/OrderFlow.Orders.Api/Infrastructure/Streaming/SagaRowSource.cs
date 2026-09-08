using System.Data;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Orders.Infrastructure.Persistence;

namespace OrderFlow.Orders.Infrastructure.Streaming;

public sealed class SagaRowSource(IServiceScopeFactory scopes) : ISagaRowSource
{
    public async Task<SagaSnapshot?> ReadAsync(Guid orderId, int afterSeq, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        // Status and replay must describe the same commit, or terminal could overtake its last row.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null) return null;
        var rows = await db.OrderSagaLog.AsNoTracking().Where(x => x.OrderId == orderId && x.Seq > afterSeq)
            .OrderBy(x => x.Seq).ToListAsync(cancellationToken);
        return new SagaSnapshot(rows.Select(SagaRow.FromEntity).ToList(), order.Status.ToString(),
            order.FailureStage?.ToString(), order.FailureReason,
            await db.OrderSagaLog.AnyAsync(x => x.OrderId == orderId && x.EventType == "StockReleased", cancellationToken));
    }
}
