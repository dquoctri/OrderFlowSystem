namespace OrderFlow.Contracts;

public sealed record EventEnvelope<T>(Guid EventId, Guid CorrelationId, Guid OrderId, DateTimeOffset Timestamp, T Data);
