using System.Net.Http.Json;
using OrderFlow.Web.Api.Models;

namespace OrderFlow.Web.Api;

public sealed class OrderFlowApiClient
{
    private readonly HttpClient httpClient;
    private readonly ApiUrls urls;

    public OrderFlowApiClient(HttpClient httpClient, ApiUrls urls)
    {
        this.httpClient = httpClient;
        this.urls = urls;
    }

    public Task<OrderCreated?> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<OrderCreated>(urls.Orders.TrimEnd('/') + "/orders", request, cancellationToken);

    public Task<OrderDetails?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<OrderDetails>(urls.Orders.TrimEnd('/') + $"/orders/{orderId:D}", cancellationToken);

    public async Task<IReadOnlyList<SagaEvent>> GetOrderTraceAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<SagaEvent>>(urls.Orders.TrimEnd('/') + $"/orders/{orderId:D}/trace", cancellationToken) ?? [];

    public async Task<IReadOnlyList<OrderSummary>> GetOrdersAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<OrderSummary>>(urls.Orders.TrimEnd('/') + "/orders", cancellationToken) ?? new List<OrderSummary>();

    public async Task<IReadOnlyList<StockItem>> GetStockAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<StockItem>>(urls.Inventory.TrimEnd('/') + "/stock", cancellationToken) ?? new List<StockItem>();

    private async Task<T?> PostAsync<T>(string url, object payload, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }
}
