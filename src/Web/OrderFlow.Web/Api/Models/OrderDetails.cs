namespace OrderFlow.Web.Api.Models;

public sealed record OrderDetails(
    Guid OrderId,
    string CustomerId,
    string Status,
    decimal TotalAmount,
    bool ReservationCompleted,
    bool PaymentCompleted,
    string? FailureStage,
    string? FailureReason,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<OrderLine> Lines,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
