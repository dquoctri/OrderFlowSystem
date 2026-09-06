using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Contracts;
using OrderFlow.Inventory.Infrastructure.Messaging;
using OrderFlow.Inventory.Infrastructure.Persistence;
using OrderFlow.Inventory.Infrastructure.Persistence.Entities;
using OrderFlow.Messaging;

namespace Inventory.Tests;

/// <summary>
/// Proves the one thing that genuinely needs a real database: <c>SELECT … FOR UPDATE</c> in
/// <c>InventoryEventConsumer.LockStockAsync</c> serialises concurrent reservations, and the inbox
/// <c>INSERT … ON CONFLICT DO NOTHING</c> makes the handler idempotent. The stock arithmetic these
/// assert on is also covered — without Docker — by <see cref="StockOperationsTests"/>.
/// Skips (does not fail) when Docker is unavailable.
/// </summary>
[Trait("Category", "Integration")]
public sealed class StockReservationConcurrencyTests : IClassFixture<InventoryDatabaseFixture>
{
    private readonly InventoryDatabaseFixture fixture;

    public StockReservationConcurrencyTests(InventoryDatabaseFixture fixture) => this.fixture = fixture;

    [SkippableFact]
    public async Task Reserve_WhenTwoOrdersRaceForTheLastUnit_OnlyOneSucceeds()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        // Arrange — drive WIDGET-01 down to a single unit on hand.
        await fixture.ResetAsync();
        await using (var seed = new InventoryDbContext(fixture.Options))
        {
            var seededStock = await seed.StockItems.SingleAsync(item => item.Sku == "WIDGET-01");
            seededStock.QuantityOnHand = 1;
            await seed.SaveChangesAsync();
        }

        await using var eventBus = new PulsarEventBus("pulsar://localhost:6650", NullLogger<PulsarEventBus>.Instance);
        var consumer = new TestOrderPlacedConsumer(eventBus);
        var first = CreateOrderPlacedMessage(Guid.NewGuid());
        var second = CreateOrderPlacedMessage(Guid.NewGuid());

        // Act — both reservations run concurrently against the one unit.
        await Task.WhenAll(
            ReserveAsync(consumer, first),
            ReserveAsync(consumer, second));

        // Assert — exactly one reservation, one success event, one failure event.
        await using var db = new InventoryDbContext(fixture.Options);
        var stock = await db.StockItems.SingleAsync(item => item.Sku == "WIDGET-01");
        Assert.Equal(1, stock.QuantityReserved);
        Assert.Equal(1, await db.Reservations.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync(message => message.EventType == nameof(ReservationSucceeded)));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(message => message.EventType == nameof(ReservationFailed)));
    }

    [SkippableFact]
    public async Task Handle_WhenTheSameEventArrivesTwice_AppliesItOnce()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        // Arrange
        await fixture.ResetAsync();
        await using var eventBus = new PulsarEventBus("pulsar://localhost:6650", NullLogger<PulsarEventBus>.Instance);
        using var services = BuildServices();
        var consumer = new TestOrderPlacedConsumer(eventBus, services.GetRequiredService<IServiceScopeFactory>());
        var message = CreateOrderPlacedMessage(Guid.NewGuid());

        // Act — deliver the same message (same EventId) twice.
        await consumer.HandleForTestAsync(message, CancellationToken.None);
        await consumer.HandleForTestAsync(message, CancellationToken.None);

        // Assert — one inbox row, one reservation, one outbox row, reserved once.
        await using var db = new InventoryDbContext(fixture.Options);
        Assert.Equal(1, await db.InboxMessages.CountAsync());
        Assert.Equal(1, await db.Reservations.CountAsync());
        Assert.Equal(1, (await db.StockItems.SingleAsync(item => item.Sku == "WIDGET-01")).QuantityReserved);
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }

    [SkippableFact]
    public async Task PaymentFailed_ReleasesTheReservationAndRestoresStockExactly()
    {
        Skip.IfNot(fixture.IsAvailable, fixture.SkipReason);

        // Arrange — an order with 2 units reserved.
        await fixture.ResetAsync();
        var orderId = Guid.NewGuid();
        await using (var seed = new InventoryDbContext(fixture.Options))
        {
            var stock = await seed.StockItems.SingleAsync(item => item.Sku == "WIDGET-01");
            stock.QuantityOnHand = 10;
            stock.QuantityReserved = 2;
            seed.Reservations.Add(new ReservationEntity
            {
                OrderId = orderId,
                Sku = "WIDGET-01",
                Quantity = 2,
                Status = ReservationStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using var eventBus = new PulsarEventBus("pulsar://localhost:6650", NullLogger<PulsarEventBus>.Instance);
        var consumer = new TestPaymentFailedConsumer(eventBus);
        var message = new WireEvent(
            nameof(PaymentFailed),
            Guid.NewGuid(),
            orderId,
            orderId,
            DateTimeOffset.UtcNow,
            JsonSerializer.SerializeToElement(new PaymentFailed("test")));

        await using (var db = new InventoryDbContext(fixture.Options))
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await consumer.ApplyForTestAsync(db, message, CancellationToken.None);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        await using var result = new InventoryDbContext(fixture.Options);
        var stockAfterRelease = await result.StockItems.SingleAsync(item => item.Sku == "WIDGET-01");
        var reservation = await result.Reservations.SingleAsync();
        Assert.Equal(10, stockAfterRelease.QuantityOnHand);
        Assert.Equal(0, stockAfterRelease.QuantityReserved);
        Assert.Equal(ReservationStatus.Released, reservation.Status);
    }

    private async Task ReserveAsync(TestOrderPlacedConsumer consumer, WireEvent message)
    {
        await using var db = new InventoryDbContext(fixture.Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await consumer.ApplyForTestAsync(db, message, CancellationToken.None);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<InventoryDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
        return services.BuildServiceProvider();
    }

    private static WireEvent CreateOrderPlacedMessage(Guid orderId) => new(
        nameof(OrderPlaced),
        Guid.NewGuid(),
        orderId,
        orderId,
        DateTimeOffset.UtcNow,
        JsonSerializer.SerializeToElement(new OrderPlaced("customer", new[] { new OrderLineContract("WIDGET-01", 1, 10m) }, 10m)));

    private sealed class TestOrderPlacedConsumer : OrderPlacedConsumer
    {
        public TestOrderPlacedConsumer(PulsarEventBus eventBus, IServiceScopeFactory? scopeFactory = null)
            : base(eventBus, scopeFactory!, NullLogger<OrderPlacedConsumer>.Instance) { }

        public Task ApplyForTestAsync(InventoryDbContext db, WireEvent message, CancellationToken cancellationToken) =>
            ApplyAsync(db, message, cancellationToken);

        public Task HandleForTestAsync(WireEvent message, CancellationToken cancellationToken) =>
            HandleAsync(message, cancellationToken);
    }

    private sealed class TestPaymentFailedConsumer : PaymentFailedConsumer
    {
        public TestPaymentFailedConsumer(PulsarEventBus eventBus)
            : base(eventBus, null!, NullLogger<PaymentFailedConsumer>.Instance) { }

        public Task ApplyForTestAsync(InventoryDbContext db, WireEvent message, CancellationToken cancellationToken) =>
            ApplyAsync(db, message, cancellationToken);
    }
}
