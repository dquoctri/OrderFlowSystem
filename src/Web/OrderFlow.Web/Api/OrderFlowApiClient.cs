using System.Net.Http.Json;
using OrderFlow.Web.Api.Models;

namespace OrderFlow.Web.Api;

/// <summary>
/// The SPA's single HTTP dependency. Every call goes to the BFF (<see cref="ApiUrls.Bff"/>),
/// which composes the Orders and Inventory services server-side.
/// </summary>
public sealed class OrderFlowApiClient
{
    private readonly HttpClient httpClient;

    public OrderFlowApiClient(HttpClient httpClient) => this.httpClient = httpClient;

    public Task<OrderCreated?> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<OrderCreated>("orders", request, cancellationToken);

    public Task<OrderDetails?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<OrderDetails>($"orders/{orderId:D}", cancellationToken);

    public async Task<IReadOnlyList<SagaEvent>> GetOrderTraceAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<SagaEvent>>($"orders/{orderId:D}/trace", cancellationToken) ?? [];

    /// <summary>One call for the whole dashboard — stock + recent orders, composed by the BFF.</summary>
    public Task<DashboardView?> GetDashboardAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<DashboardView>("dashboard", cancellationToken);

    private async Task<T?> PostAsync<T>(string url, object payload, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(url, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }
}
