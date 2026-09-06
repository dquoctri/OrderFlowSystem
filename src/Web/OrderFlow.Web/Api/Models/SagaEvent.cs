using System.Text.Json;

namespace OrderFlow.Web.Api.Models;

/// <summary>One row of <c>GET /orders/{id}/trace</c> — a saga event as Orders recorded it.</summary>
public sealed record SagaEvent(
    int Seq,
    string EventType,
    string Source,
    DateTimeOffset OccurredAt,
    DateTimeOffset RecordedAt,
    JsonElement? Detail);
