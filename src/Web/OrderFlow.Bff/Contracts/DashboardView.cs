using System.Text.Json;

namespace OrderFlow.Bff.Contracts;

/// <summary>
/// The composed payload for the dashboard screen. One BFF call replaces the
/// stock + orders round-trips the SPA used to make to two services.
/// </summary>
/// <param name="Stock">Stock rows, or <c>null</c> when the Inventory service could not be reached
/// (the rest of the dashboard still renders — see <paramref name="Warnings"/>).</param>
/// <param name="Orders">Recent orders, newest first.</param>
/// <param name="Warnings">Human-readable notes about anything that was degraded in this response.</param>
/// <param name="AsOf">When the BFF assembled this view.</param>
public sealed record DashboardView(
    IReadOnlyList<StockRow>? Stock,
    IReadOnlyList<OrderRow> Orders,
    IReadOnlyList<string> Warnings,
    DateTimeOffset AsOf);

public sealed record StockRow(string Sku, int QuantityOnHand, int QuantityReserved, int Available);

public sealed record OrderRow(Guid OrderId, string Status, decimal TotalAmount, DateTimeOffset CreatedAt);

/// <summary>Composed order detail + saga trace for the "live saga monitor" panel.</summary>
public sealed record TrackedOrderView(OrderView Order, IReadOnlyList<SagaEventView> Trace);

public sealed record OrderView(
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

public sealed record SagaEventView(
    int Seq,
    string EventType,
    string Source,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    JsonElement? Detail);
