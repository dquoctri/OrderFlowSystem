using System.Net.Http.Json;
using System.Text.Json;

namespace Integration.Tests;

[Trait("Category", "Integration")]
public sealed class ComposeSagaTests
{
    private static readonly Uri OrdersUrl = new(Environment.GetEnvironmentVariable("ORDERFLOW_ORDERS_URL") ?? "http://localhost:5001");
    private static readonly Uri InventoryUrl = new(Environment.GetEnvironmentVariable("ORDERFLOW_INVENTORY_URL") ?? "http://localhost:5002");

    [Fact]
    public async Task HappyPathConfirmsAndConsumesStock()
    {
        using var client = new HttpClient();
        await EnsureComposeStackAsync(client);
        var stockBefore = await GetStockAsync(client, "WIDGET-01");
        var orderId = await CreateOrderAsync(client, "integration-happy", "WIDGET-01", 1, 10.50m);
        var order = await WaitForTerminalOrderAsync(client, orderId);

        Assert.Equal("Confirmed", order.GetProperty("status").GetString());
        Assert.False(order.TryGetProperty("failureReason", out var reason) && reason.ValueKind == JsonValueKind.String);
        var stockAfter = await GetStockAsync(client, "WIDGET-01");
        Assert.Equal(stockBefore - 1, stockAfter);

        var trace = await GetTraceAsync(client, orderId);
        Assert.Contains(trace, entry => entry.GetProperty("eventType").GetString() == "OrderPlaced");
        Assert.Contains(trace, entry => entry.GetProperty("eventType").GetString() == "ReservationSucceeded");
        Assert.Contains(trace, entry => entry.GetProperty("eventType").GetString() == "PaymentSucceeded");
    }

    [Fact]
    public async Task NinetyNinePathCancelsAndRestoresStock()
    {
        using var client = new HttpClient();
        await EnsureComposeStackAsync(client);
        var stockBefore = await GetStockDetailsAsync(client, "WIDGET-02");
        var orderId = await CreateOrderAsync(client, "integration-failure", "WIDGET-02", 1, 5.99m);
        var order = await WaitForTerminalOrderAsync(client, orderId);

        Assert.Equal("Cancelled", order.GetProperty("status").GetString());
        Assert.Equal("PaymentDeclined", order.GetProperty("failureStage").GetString());
        Assert.False(string.IsNullOrWhiteSpace(order.GetProperty("failureReason").GetString()));

        var stockAfter = await GetStockDetailsAsync(client, "WIDGET-02");
        Assert.Equal(stockBefore.GetProperty("quantityOnHand").GetInt32(), stockAfter.GetProperty("quantityOnHand").GetInt32());
        Assert.Equal(stockBefore.GetProperty("quantityReserved").GetInt32(), stockAfter.GetProperty("quantityReserved").GetInt32());

        var trace = await GetTraceAsync(client, orderId);
        Assert.Contains(trace, entry => entry.GetProperty("eventType").GetString() == "PaymentFailed");
        Assert.Contains(trace, entry => entry.GetProperty("eventType").GetString() == "StockReleased");
    }

    private static async Task<JsonElement[]> GetTraceAsync(HttpClient client, Guid orderId) =>
        await client.GetFromJsonAsync<JsonElement[]>(new Uri(OrdersUrl, $"/orders/{orderId:D}/trace"))
            ?? throw new Xunit.Sdk.XunitException("Get trace returned an empty response.");

    private static async Task<Guid> CreateOrderAsync(HttpClient client, string customerId, string sku, int quantity, decimal unitPrice)
    {
        using var response = await client.PostAsJsonAsync(new Uri(OrdersUrl, "/orders"), new
        {
            customerId,
            lines = new[] { new { sku, quantity, unitPrice } }
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("orderId").GetGuid();
    }

    private static async Task<JsonElement> WaitForTerminalOrderAsync(HttpClient client, Guid orderId)
    {
        for (var attempt = 0; attempt < 30; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            using var response = await client.GetAsync(new Uri(OrdersUrl, $"/orders/{orderId:D}"));
            response.EnsureSuccessStatusCode();
            var order = await response.Content.ReadFromJsonAsync<JsonElement>();
            var status = order.GetProperty("status").GetString();
            if (status is "Confirmed" or "Cancelled") return order;
        }

        throw new Xunit.Sdk.XunitException($"Order {orderId} did not reach a terminal state.");
    }

    private static async Task<int> GetStockAsync(HttpClient client, string sku) =>
        (await GetStockDetailsAsync(client, sku)).GetProperty("available").GetInt32();

    private static async Task<JsonElement> GetStockDetailsAsync(HttpClient client, string sku)
    {
        var stock = await client.GetFromJsonAsync<JsonElement[]>(new Uri(InventoryUrl, "/stock"))
            ?? throw new Xunit.Sdk.XunitException("Get stock returned an empty response.");
        return stock.Single(item => item.GetProperty("sku").GetString() == sku);
    }

    private static async Task EnsureComposeStackAsync(HttpClient client)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            using var orders = await client.GetAsync(new Uri(OrdersUrl, "/health/live"), timeout.Token);
            orders.EnsureSuccessStatusCode();
            using var inventory = await client.GetAsync(new Uri(InventoryUrl, "/health/live"), timeout.Token);
            inventory.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException)
        {
            throw new Xunit.Sdk.XunitException("OrderFlow Compose stack is not running; start it with `docker compose up --build` and see docs/demo-script.md.");
        }
        catch (TaskCanceledException)
        {
            throw new Xunit.Sdk.XunitException("OrderFlow Compose stack did not respond; start it with `docker compose up --build` and see docs/demo-script.md.");
        }
    }
}
