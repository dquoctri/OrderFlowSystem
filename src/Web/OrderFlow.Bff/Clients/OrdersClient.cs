using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrderFlow.Bff.Clients;

public sealed class OrdersClient : IOrdersClient
{
    private readonly HttpClient httpClient;

    public OrdersClient(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<IReadOnlyList<UpstreamOrderSummaryDto>> GetRecentOrdersAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<List<UpstreamOrderSummaryDto>>("/orders", cancellationToken) ?? [];

    public async Task<UpstreamOrderDetailsDto?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(new Uri($"/orders/{orderId:D}", UriKind.Relative), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UpstreamOrderDetailsDto>(cancellationToken);
    }

    public async Task<IReadOnlyList<UpstreamSagaEventDto>> GetTraceAsync(Guid orderId, CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<List<UpstreamSagaEventDto>>(new Uri($"/orders/{orderId:D}/trace", UriKind.Relative), cancellationToken) ?? [];

    public async Task<UpstreamResult> GetRawAsync(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(new Uri(relativePath, UriKind.Relative), cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return new UpstreamResult((int)response.StatusCode, string.IsNullOrWhiteSpace(json) ? null : json);
    }

    public async Task<UpstreamResult> CreateOrderAsync(JsonElement body, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(new Uri("/orders", UriKind.Relative), body, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return new UpstreamResult((int)response.StatusCode, string.IsNullOrWhiteSpace(json) ? null : json);
    }
}
