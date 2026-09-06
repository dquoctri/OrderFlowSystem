using System.Text.Json;

namespace OrderFlow.Messaging;

public sealed record DeadLetterMessage(
    Guid EventId,
    Guid CorrelationId,
    Guid OrderId,
    string EventType,
    DateTimeOffset Timestamp,
    JsonElement Data);
