using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderFlow.Bff.Clients;
using OrderFlow.Bff.Composition;
using OrderFlow.Bff.Infrastructure.Options;

namespace Bff.Tests;

public sealed class DashboardComposerTests
{
    private readonly FakeOrdersClient orders = new();
    private readonly FakeInventoryClient inventory = new();

    private DashboardComposer CreateComposer(int budgetSeconds = 3)
    {
        var options = Options.Create(new DownstreamOptions
        {
            OrdersBaseUrl = "http://orders",
            InventoryBaseUrl = "http://inventory",
            RequestTimeoutSeconds = budgetSeconds
        });
        return new DashboardComposer(orders, inventory, options, NullLogger<DashboardComposer>.Instance);
    }

    [Fact]
    public async Task ComposeDashboardAsync_BothServicesHealthy_MergesStockAndOrders()
    {
        orders.RecentOrders = [new UpstreamOrderSummaryDto(Guid.NewGuid(), "Confirmed", 42m, DateTimeOffset.UtcNow)];
        inventory.Stock = [new UpstreamStockItemDto("WIDGET-01", 10, 2, 8)];

        var view = await CreateComposer().ComposeDashboardAsync(CancellationToken.None);

        Assert.Empty(view.Warnings);
        Assert.NotNull(view.Stock);
        Assert.Equal("WIDGET-01", Assert.Single(view.Stock!).Sku);
        Assert.Equal("Confirmed", Assert.Single(view.Orders).Status);
    }

    [Fact]
    public async Task ComposeDashboardAsync_InventoryFails_ReturnsOrdersWithNullStockAndWarning()
    {
        orders.RecentOrders = [new UpstreamOrderSummaryDto(Guid.NewGuid(), "Pending", 10m, DateTimeOffset.UtcNow)];
        inventory.Fail = new HttpRequestException("inventory down");

        var view = await CreateComposer().ComposeDashboardAsync(CancellationToken.None);

        Assert.Null(view.Stock);
        Assert.Single(view.Orders);
        Assert.Single(view.Warnings);
    }

    [Fact]
    public async Task ComposeDashboardAsync_OrdersFails_ThrowsUpstreamUnavailable()
    {
        orders.FailRecentOrders = new HttpRequestException("orders down");
        inventory.Stock = [new UpstreamStockItemDto("WIDGET-01", 10, 0, 10)];

        var exception = await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => CreateComposer().ComposeDashboardAsync(CancellationToken.None));
        Assert.Equal("orders", exception.Service);
    }

    [Fact]
    public async Task ComposeDashboardAsync_OrdersSlowerThanBudget_ThrowsUpstreamUnavailable()
    {
        orders.Delay = TimeSpan.FromSeconds(5);

        await Assert.ThrowsAsync<UpstreamUnavailableException>(
            () => CreateComposer(budgetSeconds: 1).ComposeDashboardAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ComposeDashboardAsync_CallsDownstreamsInParallel()
    {
        orders.Delay = TimeSpan.FromMilliseconds(300);
        inventory.Delay = TimeSpan.FromMilliseconds(300);

        var stopwatch = Stopwatch.StartNew();
        await CreateComposer().ComposeDashboardAsync(CancellationToken.None);
        stopwatch.Stop();

        // Sequential would be ~600ms; parallel is ~300ms. Generous ceiling for CI noise.
        Assert.True(stopwatch.ElapsedMilliseconds < 550, $"took {stopwatch.ElapsedMilliseconds}ms — calls did not overlap");
        Assert.True(orders.RecentOrdersStartedAt <= inventory.FinishedAt);
        Assert.True(inventory.StartedAt <= orders.RecentOrdersFinishedAt);
    }

    [Fact]
    public async Task ComposeTrackedOrderAsync_OrderNotFound_ReturnsNull()
    {
        orders.Order = null;

        var view = await CreateComposer().ComposeTrackedOrderAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(view);
    }

    [Fact]
    public async Task ComposeTrackedOrderAsync_TraceFails_ReturnsOrderWithEmptyTrace()
    {
        var id = Guid.NewGuid();
        orders.Order = new UpstreamOrderDetailsDto(id, "cust", "Confirmed", 10m, true, true, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        orders.FailTrace = new HttpRequestException("trace down");

        var view = await CreateComposer().ComposeTrackedOrderAsync(id, CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal(id, view!.Order.OrderId);
        Assert.Empty(view.Trace);
    }
}
