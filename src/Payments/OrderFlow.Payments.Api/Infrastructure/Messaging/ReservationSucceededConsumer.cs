using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;
using OrderFlow.Messaging;
using OrderFlow.Payments.Infrastructure.Persistence;
using OrderFlow.Payments.Infrastructure.Persistence.Entities;
using OrderFlow.Payments;

namespace OrderFlow.Payments.Infrastructure.Messaging;

public sealed class ReservationSucceededConsumer : PulsarConsumerWorker
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IPaymentGateway gateway;

    public ReservationSucceededConsumer(PulsarEventBus eventBus, IServiceScopeFactory scopeFactory, IPaymentGateway gateway, ILogger<ReservationSucceededConsumer> logger)
        : base(eventBus, logger)
    {
        this.scopeFactory = scopeFactory;
        this.gateway = gateway;
    }

    protected override string Topic => PulsarTopics.ReservationSucceeded;
    protected override string SubscriptionName => PulsarTopics.PaymentsSubscription;

    protected override async Task HandleAsync(WireEvent message, CancellationToken cancellationToken)
    {
        var reservation = PulsarEventBus.DeserializeData<ReservationSucceeded>(message);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "orderflow_payments"."inbox_messages" ("EventId", "ProcessedAt")
            VALUES ({message.EventId}, NOW())
            ON CONFLICT ("EventId") DO NOTHING
            """, cancellationToken);
        if (inserted == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        var paymentId = Guid.NewGuid();
        var charge = gateway.Charge(message.OrderId, reservation.Amount);
        var record = PaymentRecord.From(paymentId, reservation.Amount, charge);

        db.Payments.Add(new PaymentEntity
        {
            Id = paymentId,
            OrderId = message.OrderId,
            Amount = reservation.Amount,
            Status = record.Status,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var eventId = Guid.NewGuid();
        var envelope = new EventEnvelope<object>(eventId, message.CorrelationId, message.OrderId, DateTimeOffset.UtcNow, record.Payload);
        db.OutboxMessages.Add(new OutboxMessageEntity
        {
            EventId = eventId,
            OrderId = message.OrderId,
            CorrelationId = envelope.CorrelationId,
            EventType = record.EventType,
            Topic = record.Topic,
            Payload = JsonSerializer.Serialize(envelope),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
