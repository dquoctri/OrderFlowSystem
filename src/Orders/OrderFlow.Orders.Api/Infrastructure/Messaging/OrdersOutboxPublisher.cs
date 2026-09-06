using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderFlow.Messaging;
using OrderFlow.Orders.Infrastructure;
using OrderFlow.Orders.Infrastructure.Persistence;

namespace OrderFlow.Orders.Infrastructure.Messaging;

public sealed class OrdersOutboxPublisher : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private readonly IServiceScopeFactory scopeFactory;
    private readonly PulsarEventBus eventBus;
    private readonly ILogger<OrdersOutboxPublisher> logger;
    private readonly string owner = $"{Environment.MachineName}-{Guid.NewGuid():N}";

    public OrdersOutboxPublisher(IServiceScopeFactory scopeFactory, PulsarEventBus eventBus, ILogger<OrdersOutboxPublisher> logger)
    {
        this.scopeFactory = scopeFactory;
        this.eventBus = eventBus;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await PublishOneAsync(stoppingToken))
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogMessages.OutboxPublishingFailed(logger, exception);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task<bool> PublishOneAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await using var claimTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var message = await db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM "orderflow_orders"."outbox_messages"
            WHERE "PublishedAt" IS NULL AND ("LockExpiresAt" IS NULL OR "LockExpiresAt" < NOW())
            ORDER BY "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).SingleOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            await claimTransaction.RollbackAsync(cancellationToken);
            return false;
        }

        message.LockOwner = owner;
        message.LockExpiresAt = DateTimeOffset.UtcNow.AddSeconds(30);
        message.Attempts++;
        await db.SaveChangesAsync(cancellationToken);
        await claimTransaction.CommitAsync(cancellationToken);

        await eventBus.PublishRawAsync(message.Topic, message.EventType, message.OrderId,
            System.Text.Encoding.UTF8.GetBytes(message.Payload), message.CorrelationId, cancellationToken);
        var publishedMessage = await db.OutboxMessages.SingleAsync(value => value.Id == message.Id && value.LockOwner == owner, cancellationToken);
        publishedMessage.PublishedAt = DateTimeOffset.UtcNow;
        publishedMessage.LockOwner = null;
        publishedMessage.LockExpiresAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
