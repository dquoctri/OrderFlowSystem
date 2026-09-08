using System.Globalization;
using Microsoft.AspNetCore.Http.Features;
using OrderFlow.Bff.Clients;
using OrderFlow.Bff.Streaming;

namespace OrderFlow.Bff.Api;

public static class StreamEndpoints
{
    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/dashboard/orders/{id:guid}/stream", StreamAsync);
        return endpoints;
    }

    private static async Task StreamAsync(Guid id, HttpContext context, IOrdersClient orders, SagaStreamFanout fanout)
    {
        var header = context.Request.Headers["Last-Event-ID"].ToString();
        var cursor = 0;
        if (header.Length > 0 && !int.TryParse(header, NumberStyles.None, CultureInfo.InvariantCulture, out cursor))
        {
            context.Response.StatusCode = 400;
            return;
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        lifetime.CancelAfter(TimeSpan.FromMinutes(10));
        var token = lifetime.Token;
        try
        {
            if (await orders.GetOrderAsync(id, token) is null)
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            await WriteAsync(":ok\n\n");
            await using var enumerator = fanout.Subscribe(id, cursor, token).GetAsyncEnumerator(token);
            var next = enumerator.MoveNextAsync().AsTask();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            var tick = timer.WaitForNextTickAsync(token).AsTask();
            try
            {
                while (true)
                {
                    if (await Task.WhenAny(next, tick) == next)
                    {
                        if (!await next) break;
                        await WriteAsync(enumerator.Current.Encode());
                        next = enumerator.MoveNextAsync().AsTask();
                    }
                    else
                    {
                        if (!await tick) break;
                        await WriteAsync(": ping\n\n");
                        tick = timer.WaitForNextTickAsync(token).AsTask();
                    }
                }
            }
            finally
            {
                await lifetime.CancelAsync();
                try { await next; } catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                try { await tick; } catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }

        async Task WriteAsync(string frame)
        {
            await context.Response.WriteAsync(frame, token);
            await context.Response.Body.FlushAsync(token);
        }
    }
}
