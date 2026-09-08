using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace OrderFlow.Bff.Clients;

public sealed class OrdersClient : IOrdersClient
{
    private readonly HttpClient httpClient;

    private readonly IHttpClientFactory clients;

    public OrdersClient(HttpClient httpClient, IHttpClientFactory clients)
    {
        this.httpClient = httpClient;
        this.clients = clients;
    }

    public async Task<HttpResponseMessage> OpenTraceStreamAsync(Guid orderId, string? lastEventId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/orders/{orderId:D}/stream", UriKind.Relative));
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (lastEventId is not null) request.Headers.Add("Last-Event-ID", lastEventId);
        return await clients.CreateClient("orders-stream").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

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
