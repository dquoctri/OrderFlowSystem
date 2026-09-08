using System.Text.Json;

namespace OrderFlow.Web.Api.Models;

public static class SagaProgress
{
    public static OrderDetails Apply(OrderDetails order, string kind, JsonElement data)
    {
        if (kind == "terminal") return order with
        {
            Status = data.GetProperty("status").GetString()!,
            FailureStage = data.GetProperty("failureStage").GetString(),
            FailureReason = data.GetProperty("failureReason").GetString()
        };
        if (kind != "saga") return order;
        var eventType = data.GetProperty("eventType").GetString();
        var reason = data.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.Object &&
            detail.TryGetProperty("reason", out var value) ? value.GetString() : null;
        return eventType switch
        {
            "OrderPlaced" => order with { Status = "Reserving" },
            "ReservationSucceeded" => order with { Status = "Charging", ReservationCompleted = true },
            "PaymentSucceeded" => order with { PaymentCompleted = true },
            "PaymentFailed" => order with { Status = "Compensating", FailureStage = "PaymentDeclined", FailureReason = reason },
            "ReservationFailed" => order with { FailureStage = "ReservationRejected", FailureReason = reason },
            _ => order
        };
    }
}
public sealed record SagaStreamMessage(string Kind, JsonElement Data);
