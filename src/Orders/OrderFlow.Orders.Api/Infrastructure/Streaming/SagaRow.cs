using System.Text.Json;
using OrderFlow.Orders.Infrastructure.Persistence.Entities;

namespace OrderFlow.Orders.Infrastructure.Streaming;

public sealed record SagaRow(int Seq, string EventType, string Source, DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt, JsonElement? Detail)
{
    public static SagaRow FromEntity(OrderSagaLogEntity entry) => new(entry.Seq, entry.EventType,
        entry.Source, entry.OccurredAt, entry.RecordedAt,
        entry.Detail is null ? null : JsonSerializer.Deserialize<JsonElement>(entry.Detail));
}

public sealed record SagaSnapshot(IReadOnlyList<SagaRow> Rows, string Status, string? FailureStage, string? FailureReason,
    bool CompensationCompleted = false)
{
    // Payments marks the order Cancelled before Inventory's compensation reaches Orders.
    public bool IsTerminal => Status == "Confirmed" ||
        (Status == "Cancelled" && (FailureStage != "PaymentDeclined" || CompensationCompleted));
}

public interface ISagaRowSource
{
    Task<SagaSnapshot?> ReadAsync(Guid orderId, int afterSeq, CancellationToken cancellationToken);
}
