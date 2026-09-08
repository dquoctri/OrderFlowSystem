using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Npgsql;
using OrderFlow.Orders.Infrastructure.Options;

namespace OrderFlow.Orders.Infrastructure.Streaming;

public sealed class SagaStreamHub(ISagaRowSource source, IOptions<DatabaseOptions> database) : BackgroundService
{
    private readonly ConcurrentDictionary<Channel<bool>, Guid> subscribers = new();

    public void Signal(Guid orderId)
    {
        foreach (var (channel, id) in subscribers)
            if (id == orderId) channel.Writer.TryWrite(true);
    }

    public async IAsyncEnumerable<SagaSnapshot> Subscribe(Guid orderId, int afterSeq,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
        subscribers[channel] = orderId; // Register before replay to close the connect/insert race.
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var snapshot = await source.ReadAsync(orderId, afterSeq, cancellationToken);
                if (snapshot is null) yield break;
                foreach (var row in snapshot.Rows) afterSeq = Math.Max(afterSeq, row.Seq);
                yield return snapshot;
                if (snapshot.IsTerminal) yield break;
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                wait.CancelAfter(TimeSpan.FromSeconds(15));
                try { await channel.Reader.ReadAsync(wait.Token); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { }
            }
        }
        finally { subscribers.TryRemove(channel, out _); }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(database.Value.ConnectionString);
                connection.Notification += (_, notification) =>
                {
                    if (Guid.TryParse(notification.Payload, out var id)) Signal(id);
                };
                await connection.OpenAsync(stoppingToken);
                await using var command = new NpgsqlCommand("LISTEN order_saga_log", connection);
                await command.ExecuteNonQueryAsync(stoppingToken);
                foreach (var channel in subscribers.Keys) channel.Writer.TryWrite(true);
                while (!stoppingToken.IsCancellationRequested) await connection.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (NpgsqlException) { }
            catch (IOException) { }
            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
