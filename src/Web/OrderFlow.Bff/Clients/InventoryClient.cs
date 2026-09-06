using System.Net.Http.Json;

namespace OrderFlow.Bff.Clients;

public sealed class InventoryClient : IInventoryClient
{
    private readonly HttpClient httpClient;

    public InventoryClient(HttpClient httpClient) => this.httpClient = httpClient;

    public async Task<IReadOnlyList<UpstreamStockItemDto>> GetStockAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<List<UpstreamStockItemDto>>("/stock", cancellationToken) ?? [];
}
