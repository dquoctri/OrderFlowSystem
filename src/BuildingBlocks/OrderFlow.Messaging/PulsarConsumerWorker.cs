using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderFlow.Messaging.Resilience;

namespace OrderFlow.Messaging;

public abstract class PulsarConsumerWorker : BackgroundService
{
    private readonly PulsarEventBus eventBus;
    private readonly ILogger logger;

    protected PulsarConsumerWorker(PulsarEventBus eventBus, ILogger logger)
    {
        this.eventBus = eventBus;
        this.logger = logger;
    }

    protected abstract string Topic { get; }
    protected abstract string SubscriptionName { get; }
    protected abstract Task HandleAsync(WireEvent message, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var redeliveryTasks = new ConcurrentDictionary<Task, byte>();
        await using var consumer = eventBus.CreateConsumer(
            Topic,
            SubscriptionName,
            $"{Environment.MachineName}-{Guid.NewGuid():N}");

        try
        {
            await foreach (var message in consumer.Messages(stoppingToken))
            {
                WireEvent? wireEvent = null;
                try
                {
                    wireEvent = PulsarEventBus.DeserializeWire(message);
                    if (wireEvent.SchemaVersion != 1)
                    {
                        throw new InvalidOperationException($"Unsupported WireEvent schema version {wireEvent.SchemaVersion}.");
                    }

                    using var scope = logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["OrderId"] = wireEvent.OrderId,
                        ["CorrelationId"] = wireEvent.CorrelationId,
                        ["EventType"] = wireEvent.EventType
                    });
                    await HandleAsync(wireEvent, stoppingToken);
                    await consumer.Acknowledge(message, stoppingToken);
                    MessagingLogMessages.MessageHandled(logger, wireEvent.EventType, wireEvent.OrderId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    using var scope = logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["OrderId"] = wireEvent?.OrderId ?? ParseOrderId(message.Key),
                        ["CorrelationId"] = wireEvent?.CorrelationId,
                        ["EventType"] = wireEvent?.EventType ?? "Unknown"
                    });
                    MessagingLogMessages.MessageProcessingFailed(logger, exception, Topic, message.RedeliveryCount);

                    if (PoisonMessagePolicy.ShouldDeadLetter(message.RedeliveryCount))
                    {
                        var orderId = wireEvent?.OrderId ?? ParseOrderId(message.Key);
                        await eventBus.PublishRawAsync(
                            PulsarTopics.DeadLetter(Topic),
                            $"{wireEvent?.EventType ?? "Unknown"}.DeadLetter",
                            orderId,
                            message.Value(),
                            wireEvent?.CorrelationId,
                            stoppingToken);
                        await consumer.Acknowledge(message, stoppingToken);
                        MessagingLogMessages.MessageMovedToDeadLetter(logger, PulsarTopics.DeadLetter(Topic));
                    }
                    else
                    {
                        var redeliveryTask = RedeliverAfterDelayAsync(consumer, message, stoppingToken);
                        redeliveryTasks.TryAdd(redeliveryTask, 0);
                        _ = redeliveryTask.ContinueWith(
                            completedTask => redeliveryTasks.TryRemove(completedTask, out _),
                            CancellationToken.None,
                            TaskContinuationOptions.ExecuteSynchronously,
                            TaskScheduler.Default);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            while (!redeliveryTasks.IsEmpty)
            {
                await Task.WhenAll(redeliveryTasks.Keys.ToArray());
            }
        }
    }

    private static async Task RedeliverAfterDelayAsync(IConsumer<byte[]> consumer, IMessage<byte[]> message, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(RedeliveryPolicy.GetDelay(message.RedeliveryCount), stoppingToken);
            await consumer.RedeliverUnacknowledgedMessages(new[] { message.MessageId }, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static Guid ParseOrderId(string? key) =>
        Guid.TryParse(key, out var orderId) ? orderId : Guid.Empty;
}
