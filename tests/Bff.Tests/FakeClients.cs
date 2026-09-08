using System.Text.Json;
using OrderFlow.Bff.Clients;

namespace Bff.Tests;

/// <summary>A configurable stand-in for the Orders service.</summary>
internal sealed class FakeOrdersClient : IOrdersClient
{
    public Func<Guid, string?, CancellationToken, Task<HttpResponseMessage>> OpenStream { get; set; } =
        (_, _, _) => throw new NotSupportedException();
    public Task<HttpResponseMessage> OpenTraceStreamAsync(Guid orderId, string? lastEventId, CancellationToken cancellationToken) =>
        OpenStream(orderId, lastEventId, cancellationToken);

    public IReadOnlyList<UpstreamOrderSummaryDto> RecentOrders { get; set; } = [];
    public UpstreamOrderDetailsDto? Order { get; set; }
    public IReadOnlyList<UpstreamSagaEventDto> Trace { get; set; } = [];
    public Exception? FailRecentOrders { get; set; }
    public Exception? FailTrace { get; set; }
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public DateTimeOffset? RecentOrdersStartedAt { get; private set; }
    public DateTimeOffset? RecentOrdersFinishedAt { get; private set; }

    public async Task<IReadOnlyList<UpstreamOrderSummaryDto>> GetRecentOrdersAsync(CancellationToken cancellationToken)
    {
        RecentOrdersStartedAt = DateTimeOffset.UtcNow;
        await Task.Delay(Delay, cancellationToken);
        RecentOrdersFinishedAt = DateTimeOffset.UtcNow;
        if (FailRecentOrders is not null)
        {
            throw FailRecentOrders;
        }

        return RecentOrders;
    }

    public Task<UpstreamOrderDetailsDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        Task.FromResult(Order);

    public Task<IReadOnlyList<UpstreamSagaEventDto>> GetTraceAsync(Guid orderId, CancellationToken cancellationToken) =>
        FailTrace is not null ? Task.FromException<IReadOnlyList<UpstreamSagaEventDto>>(FailTrace) : Task.FromResult(Trace);

    public Task<UpstreamResult> GetRawAsync(string relativePath, CancellationToken cancellationToken) =>
        Task.FromResult(new UpstreamResult(200, "{}"));

    public Task<UpstreamResult> CreateOrderAsync(JsonElement body, CancellationToken cancellationToken) =>
        Task.FromResult(new UpstreamResult(202, "{}"));
}

/// <summary>A configurable stand-in for the Inventory service.</summary>
internal sealed class FakeInventoryClient : IInventoryClient
{
    public IReadOnlyList<UpstreamStockItemDto> Stock { get; set; } = [];
    public Exception? Fail { get; set; }
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    public async Task<IReadOnlyList<UpstreamStockItemDto>> GetStockAsync(CancellationToken cancellationToken)
    {
        StartedAt = DateTimeOffset.UtcNow;
        await Task.Delay(Delay, cancellationToken);
        FinishedAt = DateTimeOffset.UtcNow;
        if (Fail is not null)
        {
            throw Fail;
        }

        return Stock;
    }
}
