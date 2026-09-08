using System.Text.Json;
using Microsoft.JSInterop;
using OrderFlow.Web.Api.Models;

namespace OrderFlow.Web.Api;

public sealed class SagaStreamClient(IJSRuntime js, ApiUrls urls, OrderFlowApiClient api)
{
    public async Task<Subscription> ConnectAsync(Guid orderId, Func<string, JsonElement, Task> receive)
    {
        var subscription = new Subscription(api, orderId, receive);
        try
        {
            await subscription.ConnectAsync(js, $"{urls.Bff.TrimEnd('/')}/dashboard/orders/{orderId:D}/stream");
            return subscription;
        }
        catch
        {
            await subscription.DisposeAsync();
            throw;
        }
    }

    public sealed class Subscription(OrderFlowApiClient api, Guid orderId, Func<string, JsonElement, Task> receive) : IAsyncDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly CancellationTokenSource cancellation = new();
        private DotNetObjectReference<Subscription>? reference;
        private IJSObjectReference? module;
        private IJSObjectReference? connection;
        private Task? fallback;

        internal async Task ConnectAsync(IJSRuntime js, string url)
        {
            module = await js.InvokeAsync<IJSObjectReference>("import", "./js/sse.js");
            reference = DotNetObjectReference.Create(this);
            connection = await module.InvokeAsync<IJSObjectReference>("connect", url, reference);
        }

        [JSInvokable]
        public async Task Receive(string kind, string data)
        {
            if (cancellation.IsCancellationRequested) return;
            if (kind == "fallback")
            {
                fallback ??= PollAsync(cancellation.Token);
                return;
            }
            await receive(kind, JsonSerializer.Deserialize<JsonElement>(data));
        }

        private async Task PollAsync(CancellationToken token)
        {
            try
            {
                await receive("stalled", JsonSerializer.SerializeToElement(new { reason = "Live connection unavailable; checking every 5 seconds." }));
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var view = await api.GetTrackedOrderAsync(orderId, token);
                        if (view is not null)
                        {
                            foreach (var row in view.Trace) await receive("saga", JsonSerializer.SerializeToElement(row, JsonOptions));
                            if (view.Order.Status == "Confirmed" || (view.Order.Status == "Cancelled" &&
                                (view.Order.FailureStage != "PaymentDeclined" || view.Trace.Any(x => x.EventType == "StockReleased"))))
                            {
                                await receive("terminal", JsonSerializer.SerializeToElement(view.Order, JsonOptions));
                                return;
                            }
                        }
                    }
                    catch (HttpRequestException) { }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }

        public async ValueTask DisposeAsync()
        {
            await cancellation.CancelAsync();
            if (connection is not null)
            {
                await connection.InvokeVoidAsync("close");
                await connection.DisposeAsync();
            }
            if (fallback is not null) await fallback;
            reference?.Dispose();
            if (module is not null) await module.DisposeAsync();
            cancellation.Dispose();
        }
    }
}
