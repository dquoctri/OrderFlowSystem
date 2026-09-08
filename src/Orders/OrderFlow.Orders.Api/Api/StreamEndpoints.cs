using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using OrderFlow.Orders.Infrastructure.Streaming;

namespace OrderFlow.Orders.Api;

public static class StreamEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapStreamEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/orders/{id:guid}/stream", StreamAsync);
        return endpoints;
    }

    private static async Task StreamAsync(Guid id, HttpContext context, ISagaRowSource source, SagaStreamHub hub)
    {
        var header = context.Request.Headers["Last-Event-ID"].ToString();
        var cursor = 0;
        if (header.Length > 0 && (!int.TryParse(header, NumberStyles.None, CultureInfo.InvariantCulture, out cursor) || cursor < 0))
        {
            context.Response.StatusCode = 400;
            return;
        }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        lifetime.CancelAfter(TimeSpan.FromMinutes(10));
        var token = lifetime.Token;
        try
        {
            if (await source.ReadAsync(id, cursor, token) is null)
            {
                context.Response.StatusCode = 404;
                return;
            }
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            context.Response.Headers["X-Accel-Buffering"] = "no";
            context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            await WriteAsync(":ok\n\n");
            await foreach (var snapshot in hub.Subscribe(id, cursor, token))
            {
                foreach (var row in snapshot.Rows)
                {
                    cursor = row.Seq;
                    await WriteAsync($"id: {cursor}\nevent: saga\ndata: {JsonSerializer.Serialize(row, JsonOptions)}\n\n");
                }
                if (snapshot.IsTerminal)
                    await WriteAsync($"id: {cursor}\nevent: terminal\ndata: {JsonSerializer.Serialize(new { snapshot.Status, snapshot.FailureStage, snapshot.FailureReason }, JsonOptions)}\n\n");
                else await WriteAsync(": ping\n\n");
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
