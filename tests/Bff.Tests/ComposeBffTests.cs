using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Bff.Tests;

/// <summary>
/// End-to-end checks against the BFF running in the Compose stack. Skips (does not fail) when the
/// stack is not reachable — mirrors <c>Integration.Tests/ComposeSagaTests</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ComposeBffTests
{
    private static readonly Uri BffUrl = new(Environment.GetEnvironmentVariable("ORDERFLOW_BFF_URL") ?? "http://localhost:5004");

    [SkippableFact]
    public async Task Dashboard_ComposesStockAndOrders()
    {
        using var client = new HttpClient { BaseAddress = new Uri(BffUrl.ToString().TrimEnd('/') + "/") };
        await SkipUnlessBffIsUpAsync(client);

        // Place one order so there is something to compose.
        using var created = await client.PostAsJsonAsync("orders", new
        {
            customerId = "bff-integration",
            lines = new[] { new { sku = "WIDGET-01", quantity = 1, unitPrice = 10.50m } }
        });
        created.EnsureSuccessStatusCode();

        var dashboard = await client.GetFromJsonAsync<JsonElement>("dashboard");

        Assert.Equal(JsonValueKind.Array, dashboard.GetProperty("stock").ValueKind);
        Assert.Contains(dashboard.GetProperty("stock").EnumerateArray(),
            item => item.GetProperty("sku").GetString() == "WIDGET-01");
        Assert.True(dashboard.GetProperty("orders").GetArrayLength() > 0);
        Assert.Equal(JsonValueKind.Array, dashboard.GetProperty("warnings").ValueKind);
    }

    [SkippableFact]
    public async Task TrackedOrder_ComposesOrderAndTrace()
    {
        using var client = new HttpClient { BaseAddress = new Uri(BffUrl.ToString().TrimEnd('/') + "/") };
        await SkipUnlessBffIsUpAsync(client);

        using var created = await client.PostAsJsonAsync("orders", new
        {
            customerId = "bff-integration-trace",
            lines = new[] { new { sku = "WIDGET-01", quantity = 1, unitPrice = 12.00m } }
        });
        created.EnsureSuccessStatusCode();
        var orderId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetGuid();

        JsonElement view = default;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            using var response = await client.GetAsync($"dashboard/orders/{orderId:D}");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                continue;
            }

            response.EnsureSuccessStatusCode();
            view = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (view.GetProperty("trace").GetArrayLength() > 0)
            {
                break;
            }
        }

        Assert.Equal(orderId, view.GetProperty("order").GetProperty("orderId").GetGuid());
        Assert.Contains(view.GetProperty("trace").EnumerateArray(),
            entry => entry.GetProperty("eventType").GetString() == "OrderPlaced");
    }

    private static async Task SkipUnlessBffIsUpAsync(HttpClient client)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var response = await client.GetAsync("health/live", timeout.Token);
            Skip.IfNot(response.IsSuccessStatusCode, "BFF responded but is not healthy.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Skip.If(true, "BFF is not reachable; start the stack with `docker compose up --build`.");
        }
    }
}
