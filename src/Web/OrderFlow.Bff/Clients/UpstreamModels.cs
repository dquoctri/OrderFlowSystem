using System.Text.Json;

namespace OrderFlow.Bff.Clients;

// Shapes returned by the downstream services. The composer maps these onto the BFF's own
// Contracts, so a downstream JSON change is absorbed here and never reaches the browser.

public sealed record UpstreamOrderSummaryDto(Guid OrderId, string Status, decimal TotalAmount, DateTimeOffset CreatedAt);

public sealed record UpstreamOrderDetailsDto(
    Guid OrderId,
    string CustomerId,
    string Status,
    decimal TotalAmount,
    bool ReservationCompleted,
    bool PaymentCompleted,
    string? FailureStage,
    string? FailureReason,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record UpstreamSagaEventDto(
    int Seq,
    string EventType,
    string Source,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    JsonElement? Detail);

public sealed record UpstreamStockItemDto(string Sku, int QuantityOnHand, int QuantityReserved, int Available);
