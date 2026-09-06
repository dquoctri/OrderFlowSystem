using System.Text.Json;

namespace OrderFlow.Messaging;

public sealed record WireEvent(
    string EventType,
    Guid EventId,
    Guid CorrelationId,
    Guid OrderId,
    DateTimeOffset Timestamp,
    JsonElement Data,
    int SchemaVersion = 1);
