using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using OrderFlow.Bff.Clients;

namespace OrderFlow.Bff.Streaming;

public sealed class SagaStreamFanout(IServiceScopeFactory scopes) : IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Broadcaster> broadcasters = [];
    private bool disposed;

    public async IAsyncEnumerable<SseFrame> Subscribe(Guid orderId, int afterSeq,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<SseFrame>(128);
        Broadcaster broadcaster;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!broadcasters.TryGetValue(orderId, out broadcaster!))
            {
                broadcaster = new Broadcaster();
                broadcasters.Add(orderId, broadcaster);
            }
            foreach (var frame in broadcaster.History) channel.Writer.TryWrite(frame);
            if (broadcaster.Terminal) channel.Writer.TryComplete();
            broadcaster.Subscribers.Add(channel);
            broadcaster.Worker ??= Task.Run(() => RunAsync(orderId, broadcaster), CancellationToken.None);
        }
        try
        {
            await foreach (var frame in channel.Reader.ReadAllAsync(cancellationToken))
            {
                if (frame.Event == "saga" && int.TryParse(frame.Id, CultureInfo.InvariantCulture, out var seq) && seq <= afterSeq) continue;
                yield return frame;
            }
        }
        finally
        {
            Task? worker = null;
            lock (gate)
            {
                broadcaster.Subscribers.Remove(channel);
                if (broadcaster.Subscribers.Count == 0)
                {
                    broadcasters.Remove(orderId);
                    broadcaster.Stop.Cancel();
                    worker = broadcaster.Worker;
                }
            }
            if (worker is not null)
            {
                await worker;
                broadcaster.Stop.Dispose();
            }
        }
    }

    private async Task RunAsync(Guid orderId, Broadcaster broadcaster)
    {
        var token = broadcaster.Stop.Token;
        string? cursor = null;
        var retrySeconds = 1;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var client = scope.ServiceProvider.GetRequiredService<IOrdersClient>();
                    using var response = await client.OpenTraceStreamAsync(orderId, cursor, token);
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentType?.MediaType != "text/event-stream")
                        throw new IOException("Orders did not return an event stream.");
                    await using var stream = await response.Content.ReadAsStreamAsync(token);
                    await foreach (var frame in SseFrame.ReadAsync(stream, token))
                    {
                        if (frame.Id is not null) cursor = frame.Id;
                        Publish(broadcaster, frame);
                        retrySeconds = 1;
                        if (frame.Event == "terminal") return;
                    }
                }
                catch (HttpRequestException) { }
                catch (IOException) { }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                if (token.IsCancellationRequested) break;
                Publish(broadcaster, new SseFrame(null, "stalled", "{\"reason\":\"Orders stream disconnected; reconnecting.\"}"));
                await Task.Delay(TimeSpan.FromSeconds(retrySeconds), token);
                retrySeconds = Math.Min(retrySeconds * 2, 15);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            lock (gate)
                foreach (var subscriber in broadcaster.Subscribers) subscriber.Writer.TryComplete();
        }
    }

    private void Publish(Broadcaster broadcaster, SseFrame frame)
    {
        lock (gate)
        {
            if (frame.Event is "saga" or "terminal") broadcaster.History.Add(frame);
            broadcaster.Terminal = frame.Event == "terminal";
            foreach (var subscriber in broadcaster.Subscribers)
                if (!subscriber.Writer.TryWrite(frame))
                    subscriber.Writer.TryComplete(); // Slow clients reconnect and replay; never silently drop saga rows.
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            foreach (var broadcaster in broadcasters.Values) broadcaster.Stop.Cancel();
        }
    }

    private sealed class Broadcaster
    {
        public List<SseFrame> History { get; } = [];
        public HashSet<Channel<SseFrame>> Subscribers { get; } = [];
        public CancellationTokenSource Stop { get; } = new();
        public Task? Worker { get; set; }
        public bool Terminal { get; set; }
    }
}
