using System.Text.Json;

namespace OrderFlow.Bff.Clients;

/// <summary>Typed access to the Orders service. One method per call the BFF needs — nothing else.</summary>
public interface IOrdersClient
{
    Task<HttpResponseMessage> OpenTraceStreamAsync(Guid orderId, string? lastEventId, CancellationToken cancellationToken);

    // --- composition: deserialised, mapped onto BFF contracts ---

    Task<IReadOnlyList<UpstreamOrderSummaryDto>> GetRecentOrdersAsync(CancellationToken cancellationToken);

    /// <returns>The order, or <c>null</c> if Orders responded 404.</returns>
    Task<UpstreamOrderDetailsDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UpstreamSagaEventDto>> GetTraceAsync(Guid orderId, CancellationToken cancellationToken);

    // --- pass-through: relay status + JSON body unchanged, so the SPA has a single origin ---

    Task<UpstreamResult> GetRawAsync(string relativePath, CancellationToken cancellationToken);

    Task<UpstreamResult> CreateOrderAsync(JsonElement body, CancellationToken cancellationToken);
}

/// <summary>A relayed downstream response for pass-through endpoints.</summary>
public sealed record UpstreamResult(int StatusCode, string? Json);
