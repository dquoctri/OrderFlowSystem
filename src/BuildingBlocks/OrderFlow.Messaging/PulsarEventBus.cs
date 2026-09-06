using System.Collections.Concurrent;
using System.Text.Json;
using DotPulsar;
using DotPulsar.Abstractions;
using DotPulsar.Extensions;
using Microsoft.Extensions.Logging;
using OrderFlow.Contracts;

namespace OrderFlow.Messaging;

public sealed class PulsarEventBus : IAsyncDisposable
{
    private readonly IPulsarClient client;
    private readonly ConcurrentDictionary<string, IProducer<byte[]>> producers = new();
    private readonly ILogger<PulsarEventBus> logger;

    public PulsarEventBus(string serviceUrl, ILogger<PulsarEventBus> logger)
    {
        client = PulsarClient.Builder().ServiceUrl(new Uri(serviceUrl)).Build();
        this.logger = logger;
    }

    public IConsumer<byte[]> CreateConsumer(string topic, string subscriptionName, string consumerName) =>
        client.NewConsumer(Schema.ByteArray)
            .ConsumerName(consumerName)
            .SubscriptionName(subscriptionName)
            .SubscriptionType(SubscriptionType.KeyShared)
            .InitialPosition(SubscriptionInitialPosition.Earliest)
            .Topic(topic)
            .Create();

    public async ValueTask PublishAsync<T>(string topic, string eventType, EventEnvelope<T> envelope, CancellationToken cancellationToken = default)
    {
        var wireEvent = new WireEvent(
            eventType,
            envelope.EventId,
            envelope.CorrelationId,
            envelope.OrderId,
            envelope.Timestamp,
            JsonSerializer.SerializeToElement(envelope.Data));
        await PublishRawAsync(topic, eventType, envelope.OrderId, JsonSerializer.SerializeToUtf8Bytes(wireEvent), envelope.CorrelationId, cancellationToken);
    }

    public async ValueTask PublishRawAsync(string topic, string eventType, Guid orderId, byte[] payload, Guid? correlationId = null, CancellationToken cancellationToken = default)
    {
        payload = NormalizePayload(eventType, payload);
        var producer = GetProducer(topic);
        await producer.NewMessage()
            .Key(orderId.ToString("D"))
            .Property("event-type", eventType)
            .Send(payload, cancellationToken);

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["OrderId"] = orderId,
            ["CorrelationId"] = correlationId,
            ["EventType"] = eventType
        });
        MessagingLogMessages.MessagePublished(logger, eventType, topic, orderId);
    }

    public async Task<IReadOnlyList<DeadLetterMessage>> ReadDeadLettersAsync(string sourceTopic, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);
        await using var consumer = CreateConsumer(PulsarTopics.DeadLetter(sourceTopic), "orderflow.dlq.inspect.v1", $"{Environment.MachineName}-dlq-inspect-{Guid.NewGuid():N}");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var messages = new List<DeadLetterMessage>();
        try
        {
            await foreach (var message in consumer.Messages(timeout.Token))
            {
                var wireEvent = DeserializeWire(message);
                messages.Add(new DeadLetterMessage(wireEvent.EventId, wireEvent.CorrelationId, wireEvent.OrderId, wireEvent.EventType, wireEvent.Timestamp, wireEvent.Data));
                if (messages.Count == limit) break;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return messages;
    }

    public async Task<int> ReplayDeadLettersAsync(string sourceTopic, int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);
        await using var consumer = CreateConsumer(PulsarTopics.DeadLetter(sourceTopic), "orderflow.dlq.replay.v1", $"{Environment.MachineName}-dlq-replay-{Guid.NewGuid():N}");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var replayed = 0;
        try
        {
            await foreach (var message in consumer.Messages(timeout.Token))
            {
                var wireEvent = DeserializeWire(message);
                await PublishRawAsync(sourceTopic, wireEvent.EventType, wireEvent.OrderId, message.Value(), wireEvent.CorrelationId, cancellationToken);
                await consumer.Acknowledge(message, cancellationToken);
                replayed++;
                if (replayed == limit) break;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        return replayed;
    }

    public static T Deserialize<T>(IMessage<byte[]> message) =>
        JsonSerializer.Deserialize<T>(message.Value()) ?? throw new JsonException("The Pulsar message payload was empty.");

    public static T Deserialize<T>(byte[] payload) =>
        JsonSerializer.Deserialize<T>(payload) ?? throw new JsonException("The Pulsar message payload was empty.");

    public static WireEvent DeserializeWire(IMessage<byte[]> message) => Deserialize<WireEvent>(message);

    public static T DeserializeData<T>(WireEvent wireEvent)
    {
        return wireEvent.Data.Deserialize<T>() ?? throw new JsonException($"The {wireEvent.EventType} event payload was empty.");
    }

    private IProducer<byte[]> GetProducer(string topic) =>
        producers.GetOrAdd(topic, key => client.NewProducer(Schema.ByteArray).Topic(key).Create());

    private static byte[] NormalizePayload(string eventType, byte[] payload)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("EventType", out _) ||
                document.RootElement.TryGetProperty("eventType", out _))
            {
                return payload;
            }

            var envelope = JsonSerializer.Deserialize<EventEnvelope<JsonElement>>(payload)
                ?? throw new JsonException("The outbox envelope was empty.");
            var wireEvent = new WireEvent(
                eventType,
                envelope.EventId,
                envelope.CorrelationId,
                envelope.OrderId,
                envelope.Timestamp,
                envelope.Data);
            return JsonSerializer.SerializeToUtf8Bytes(wireEvent);
        }
        catch (JsonException)
        {
            return payload;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var producer in producers.Values)
        {
            await producer.DisposeAsync();
        }

        await client.DisposeAsync();
    }
}
