using System.Text.Json.Serialization;

namespace OrderFlow.Web.Api.Models;

public sealed record OrderSummary(
    Guid OrderId,
    [property: JsonConverter(typeof(OrderStatusJsonConverter))] string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt);
