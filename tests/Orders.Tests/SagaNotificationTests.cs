using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderFlow.Orders.Infrastructure.Options;
using OrderFlow.Orders.Infrastructure.Persistence;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;
using OrderFlow.Orders.Infrastructure.Streaming;
using Testcontainers.PostgreSql;

namespace Orders.Tests;

[Trait("Category", "Integration")]
public sealed class SagaNotificationTests
{
    [SkippableFact]
    public async Task Trigger_CommittedInsert_NotifiesListenerAndStreamsRow()
    {
        Skip.IfNot(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")) ||
            File.Exists("/var/run/docker.sock") || File.Exists(@"\\.\pipe\docker_engine") ||
            File.Exists(@"\\.\pipe\dockerDesktopLinuxEngine"), "Docker is unavailable for Testcontainers.");
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var connectionString = postgres.GetConnectionString();
        using var services = new ServiceCollection().AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "orderflow_orders")))
            .BuildServiceProvider();
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await db.Database.MigrateAsync();
        var id = Guid.NewGuid();
        db.Orders.Add(new OrderEntity
        {
            Id = id, CustomerId = "sse-test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            SagaState = new OrderSagaStateEntity { OrderId = id }
        });
        await db.SaveChangesAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var listener = new NpgsqlConnection(connectionString);
        await listener.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand("LISTEN order_saga_log", listener);
        await command.ExecuteNonQueryAsync(timeout.Token);
        var notified = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        listener.Notification += (_, notification) => notified.TrySetResult(notification.Payload);

        using var hub = new SagaStreamHub(new SagaRowSource(services.GetRequiredService<IServiceScopeFactory>()),
            Options.Create(new DatabaseOptions { ConnectionString = connectionString }));
        await hub.StartAsync(timeout.Token);
        await using var stream = hub.Subscribe(id, 0, timeout.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        Assert.Empty(stream.Current.Rows);
        var next = stream.MoveNextAsync().AsTask();
        db.OrderSagaLog.Add(new OrderSagaLogEntity
        {
            OrderId = id, Seq = 1, EventType = "OrderPlaced", Source = "orders",
            OccurredAt = DateTimeOffset.UtcNow, RecordedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(timeout.Token);
        await listener.WaitAsync(timeout.Token);
        Assert.Equal(id.ToString(), await notified.Task.WaitAsync(timeout.Token));
        Assert.True(await next);
        // LISTEN becoming ready can also signal an empty catch-up snapshot.
        while (stream.Current.Rows.Count == 0) Assert.True(await stream.MoveNextAsync());
        Assert.Equal("OrderPlaced", Assert.Single(stream.Current.Rows).EventType);
        await hub.StopAsync(timeout.Token);
    }
}
