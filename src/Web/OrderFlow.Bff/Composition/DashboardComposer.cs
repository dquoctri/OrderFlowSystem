using Microsoft.Extensions.Options;
using OrderFlow.Bff.Clients;
using OrderFlow.Bff.Contracts;
using OrderFlow.Bff.Infrastructure.Logging;
using OrderFlow.Bff.Infrastructure.Options;

namespace OrderFlow.Bff.Composition;

/// <summary>
/// The API Composer. Fans out to the services that own each slice of a screen, in parallel,
/// under one deadline, and joins the results into a single BFF-owned view model.
///
/// Failure policy is per field:
///   - Orders is <b>required</b> for both views — if it fails the whole request is a 503.
///   - Inventory / trace are <b>degradable</b> — on failure the view returns without them plus a warning.
/// </summary>
public sealed class DashboardComposer
{
    private readonly IOrdersClient orders;
    private readonly IInventoryClient inventory;
    private readonly TimeSpan requestBudget;
    private readonly ILogger<DashboardComposer> logger;

    public DashboardComposer(
        IOrdersClient orders,
        IInventoryClient inventory,
        IOptions<DownstreamOptions> options,
        ILogger<DashboardComposer> logger)
    {
        this.orders = orders;
        this.inventory = inventory;
        this.logger = logger;
        requestBudget = TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds);
    }

    public async Task<DashboardView> ComposeDashboardAsync(CancellationToken cancellationToken)
    {
        using var budget = CreateBudget(cancellationToken);
        var token = budget.Token;

        // Start both calls before awaiting either — latency is max(orders, stock), not the sum.
        var stockTask = TryGetStockAsync(token);
        var ordersTask = orders.GetRecentOrdersAsync(token);

        IReadOnlyList<UpstreamOrderSummaryDto> orderList;
        try
        {
            orderList = await ordersTask;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await Observe(stockTask);
            throw;
        }
        catch (Exception exception)
        {
            await Observe(stockTask);
            throw Fatal("orders", exception);
        }

        var stock = await stockTask;
        var warnings = new List<string>();
        if (stock.Warning is not null)
        {
            warnings.Add(stock.Warning);
        }

        // StockRow / OrderRow deliberately keep the downstream field names so the SPA binds them
        // to its existing StockItem / OrderSummary view models with no converter.
        return new DashboardView(
            stock.Value?.Select(item => new StockRow(item.Sku, item.QuantityOnHand, item.QuantityReserved, item.Available)).ToList(),
            orderList.Select(order => new OrderRow(order.OrderId, order.Status, order.TotalAmount, order.CreatedAt)).ToList(),
            warnings,
            DateTimeOffset.UtcNow);
    }

    /// <returns>The composed view, or <c>null</c> when Orders does not know the order (404).</returns>
    public async Task<TrackedOrderView?> ComposeTrackedOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        using var budget = CreateBudget(cancellationToken);
        var token = budget.Token;

        var traceTask = TryGetTraceAsync(orderId, token);

        UpstreamOrderDetailsDto? order;
        try
        {
            order = await orders.GetOrderAsync(orderId, token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await Observe(traceTask);
            throw;
        }
        catch (Exception exception)
        {
            await Observe(traceTask);
            throw Fatal("orders", exception);
        }

        if (order is null)
        {
            await Observe(traceTask);
            return null;
        }

        var trace = await traceTask;
        var events = (trace.Value ?? [])
            .Select(entry => new SagaEventView(entry.Seq, entry.EventType, entry.Source, entry.OccurredAt, entry.RecordedAt, entry.Detail))
            .ToList();

        return new TrackedOrderView(
            new OrderView(
                order.OrderId,
                order.CustomerId,
                order.Status,
                order.TotalAmount,
                order.ReservationCompleted,
                order.PaymentCompleted,
                order.FailureStage,
                order.FailureReason,
                order.CompletedAt,
                order.CreatedAt,
                order.UpdatedAt),
            events);
    }

    private CancellationTokenSource CreateBudget(CancellationToken cancellationToken)
    {
        var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(requestBudget);
        return budget;
    }

    private async Task<CompositionResult<IReadOnlyList<UpstreamStockItemDto>>> TryGetStockAsync(CancellationToken token)
    {
        try
        {
            return CompositionResult.Ok<IReadOnlyList<UpstreamStockItemDto>>(await inventory.GetStockAsync(token));
        }
        catch (Exception exception)
        {
            BffLog.DownstreamDegraded(logger, "inventory", exception);
            return CompositionResult.Failed<IReadOnlyList<UpstreamStockItemDto>>("Inventory is unavailable — stock is not shown.");
        }
    }

    private async Task<CompositionResult<IReadOnlyList<UpstreamSagaEventDto>>> TryGetTraceAsync(Guid orderId, CancellationToken token)
    {
        try
        {
            return CompositionResult.Ok<IReadOnlyList<UpstreamSagaEventDto>>(await orders.GetTraceAsync(orderId, token));
        }
        catch (Exception exception)
        {
            BffLog.DownstreamDegraded(logger, "orders/trace", exception);
            return CompositionResult.Failed<IReadOnlyList<UpstreamSagaEventDto>>("The event trace is temporarily unavailable.");
        }
    }

    private UpstreamUnavailableException Fatal(string service, Exception exception)
    {
        BffLog.DownstreamFatal(logger, service, exception);
        return UpstreamUnavailableException.For(service, exception);
    }

    private static async Task Observe(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // the Try* wrapper already logged; observed here only to avoid an unobserved task exception
        }
    }
}
