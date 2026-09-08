using System.IO.Pipelines;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Bff.Clients;
using OrderFlow.Bff.Streaming;

namespace Bff.Tests;

public sealed class SagaStreamFanoutTests
{
    [Fact]
    public async Task Subscribe_TwoSubscribersAndLateJoiner_ShareUpstreamAndReplay()
    {
        var pipe = new Pipe();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var client = new FakeOrdersClient
        {
            OpenStream = (_, _, _) =>
            {
                Interlocked.Increment(ref calls);
                opened.TrySetResult();
                return Task.FromResult(Response(pipe.Reader.AsStream()));
            }
        };
        using var services = new ServiceCollection().AddSingleton<IOrdersClient>(client).BuildServiceProvider();
        using var fanout = new SagaStreamFanout(services.GetRequiredService<IServiceScopeFactory>());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var id = Guid.NewGuid();
        await using var first = fanout.Subscribe(id, 0, timeout.Token).GetAsyncEnumerator();
        var next = first.MoveNextAsync().AsTask();
        await opened.Task.WaitAsync(timeout.Token);
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("id: 1\nevent: saga\ndata: {}\n\n"), timeout.Token);
        Assert.True(await next);
        await using var second = fanout.Subscribe(id, 0, timeout.Token).GetAsyncEnumerator();
        Assert.True(await second.MoveNextAsync());
        Assert.Equal(first.Current, second.Current);
        Assert.Equal(1, calls);

        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("id: 2\nevent: terminal\ndata: {}\n\n"), timeout.Token);
        Assert.True(await first.MoveNextAsync());
        Assert.Equal("terminal", first.Current.Event);
        Assert.True(await second.MoveNextAsync());
        Assert.False(await first.MoveNextAsync());
        Assert.False(await second.MoveNextAsync());
        await pipe.Writer.CompleteAsync();
    }

    [Fact]
    public async Task Subscribe_UpstreamDrops_EmitsStalledAndResumesLastId()
    {
        var cursors = new List<string?>();
        var client = new FakeOrdersClient
        {
            OpenStream = (_, cursor, _) =>
            {
                cursors.Add(cursor);
                var frames = cursors.Count == 1 ? "id: 1\nevent: saga\ndata: {}\n\n" :
                    "id: 2\nevent: saga\ndata: {}\n\nid: 2\nevent: terminal\ndata: {}\n\n";
                return Task.FromResult(Response(new MemoryStream(Encoding.UTF8.GetBytes(frames))));
            }
        };
        using var services = new ServiceCollection().AddSingleton<IOrdersClient>(client).BuildServiceProvider();
        using var fanout = new SagaStreamFanout(services.GetRequiredService<IServiceScopeFactory>());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var received = new List<SseFrame>();
        await foreach (var frame in fanout.Subscribe(Guid.NewGuid(), 1, timeout.Token)) received.Add(frame);
        Assert.Equal(new[] { "stalled", "saga", "terminal" }, received.Select(x => x.Event));
        Assert.Equal(new string?[] { null, "1" }, cursors);
    }

    [Fact]
    public async Task Subscribe_LastSubscriberLeaves_DisposesUpstream()
    {
        var pipe = new Pipe();
        var stream = pipe.Reader.AsStream();
        var content = new TrackedContent(stream);
        content.Headers.ContentType = new("text/event-stream");
        var client = new FakeOrdersClient { OpenStream = (_, _, _) => Task.FromResult(new HttpResponseMessage { Content = content }) };
        using var services = new ServiceCollection().AddSingleton<IOrdersClient>(client).BuildServiceProvider();
        using var fanout = new SagaStreamFanout(services.GetRequiredService<IServiceScopeFactory>());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var subscription = fanout.Subscribe(Guid.NewGuid(), 0, timeout.Token).GetAsyncEnumerator();
        var next = subscription.MoveNextAsync().AsTask();
        await pipe.Writer.WriteAsync(Encoding.UTF8.GetBytes("id: 1\nevent: saga\ndata: {}\n\n"), timeout.Token);
        Assert.True(await next);
        await subscription.DisposeAsync();
        Assert.True(content.Disposed);
        await pipe.Writer.CompleteAsync();
    }

    [Fact]
    public async Task ReadAsync_CommentsMultilineAndIncompleteFrame_ParsesOnlyCompleteEvents()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(":ok\r\n\r\nid: 7\r\nevent: saga\r\ndata: one\r\ndata: two\r\n\r\ndata: incomplete"));
        var frames = new List<SseFrame>();
        await foreach (var frame in SseFrame.ReadAsync(stream, CancellationToken.None)) frames.Add(frame);
        Assert.Equal(new SseFrame("7", "saga", "one\ntwo"), Assert.Single(frames));
    }

    private static HttpResponseMessage Response(Stream stream)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(stream) };
        response.Content.Headers.ContentType = new("text/event-stream");
        return response;
    }

    private sealed class TrackedContent(Stream stream) : StreamContent(stream)
    {
        public bool Disposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
