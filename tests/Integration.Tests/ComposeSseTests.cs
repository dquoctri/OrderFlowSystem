using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Integration.Tests;

[Trait("Category", "Integration")]
public sealed class ComposeSseTests
{
    private static readonly Uri BffUrl = new(Environment.GetEnvironmentVariable("ORDERFLOW_BFF_URL") ?? "http://localhost:5004");
    private static readonly Uri OrdersUrl = new(Environment.GetEnvironmentVariable("ORDERFLOW_ORDERS_URL") ?? "http://localhost:5001");

    [Theory]
    [InlineData("10.50", "Confirmed")]
    [InlineData("10.99", "Cancelled")]
    public async Task Stream_SagaAndResume_OrderedFramesAndTerminal(string price, string status)
    {
        using var client = new HttpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var created = await client.PostAsJsonAsync(new Uri(BffUrl, "/orders"), new
        {
            customerId = "sse-integration",
            lines = new[] { new { sku = "WIDGET-02", quantity = 1, unitPrice = decimal.Parse(price, CultureInfo.InvariantCulture) } }
        }, timeout.Token);
        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
        var id = body.GetProperty("orderId").GetGuid();
        var frames = await ReadAsync(client, new Uri(BffUrl, $"/dashboard/orders/{id}/stream"), 0, timeout.Token);
        var saga = frames.Where(x => x.Event == "saga").ToList();
        var expected = status == "Confirmed" ? new[] { "OrderPlaced", "ReservationSucceeded", "PaymentSucceeded" } :
            new[] { "OrderPlaced", "ReservationSucceeded", "PaymentFailed", "StockReleased" };
        Assert.Equal(expected, saga.Select(x => x.Data.GetProperty("eventType").GetString()));
        Assert.Equal(Enumerable.Range(1, expected.Length), saga.Select(x => x.Id));
        Assert.Equal("terminal", frames[^1].Event);
        Assert.Equal(status, frames[^1].Data.GetProperty("status").GetString());

        foreach (var uri in new[] { new Uri(BffUrl, $"/dashboard/orders/{id}/stream"), new Uri(OrdersUrl, $"/orders/{id}/stream") })
        {
            var resumed = await ReadAsync(client, uri, 2, timeout.Token);
            Assert.Equal(expected.Skip(2), resumed.Where(x => x.Event == "saga").Select(x => x.Data.GetProperty("eventType").GetString()));
            Assert.All(resumed.Where(x => x.Event == "saga"), frame => Assert.True(frame.Id > 2));
            Assert.Equal("terminal", resumed[^1].Event);
        }
    }

    [Fact]
    public async Task Stream_UnknownOrderAndInvalidCursor_ReturnsHttpErrors()
    {
        using var client = new HttpClient();
        foreach (var uri in new[] { new Uri(BffUrl, $"/dashboard/orders/{Guid.NewGuid()}/stream"), new Uri(OrdersUrl, $"/orders/{Guid.NewGuid()}/stream") })
        {
            using var missing = await client.GetAsync(uri);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("Last-Event-ID", "-1");
            using var invalid = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }

    private static async Task<List<Frame>> ReadAsync(HttpClient client, Uri uri, int cursor, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Last-Event-ID", cursor.ToString(CultureInfo.InvariantCulture));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var reader = new StreamReader(stream);
        var frames = new List<Frame>();
        var id = 0;
        var name = "";
        string? data = null;
        while (await reader.ReadLineAsync(token) is { } line)
        {
            if (line.StartsWith("id: ", StringComparison.Ordinal)) id = int.Parse(line[4..], CultureInfo.InvariantCulture);
            if (line.StartsWith("event: ", StringComparison.Ordinal)) name = line[7..];
            if (line.StartsWith("data: ", StringComparison.Ordinal)) data = line[6..];
            if (line.Length == 0 && data is not null)
            {
                frames.Add(new Frame(id, name, JsonSerializer.Deserialize<JsonElement>(data)));
                data = null;
            }
        }
        return frames;
    }

    private sealed record Frame(int Id, string Event, JsonElement Data);
}
