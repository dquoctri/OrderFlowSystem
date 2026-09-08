using Microsoft.Extensions.Options;
using OrderFlow.Orders.Infrastructure.Options;
using OrderFlow.Orders.Infrastructure.Streaming;

namespace Orders.Tests;

public sealed class SagaStreamHubTests
{
    [Fact]
    public async Task Subscribe_ReplayAndCoalescedSignal_AdvancesCursorWithoutLosingRows()
    {
        var id = Guid.NewGuid();
        var source = new FakeSource();
        source.Rows.Add(Row(1));
        source.Rows.Add(Row(2));
        using var hub = new SagaStreamHub(source, Options.Create(new DatabaseOptions()));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await using var stream = hub.Subscribe(id, 1, timeout.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal(2, Assert.Single(stream.Current.Rows).Seq);

        source.Rows.Add(Row(3));
        source.Rows.Add(Row(4));
        source.Status = "Confirmed";
        hub.Signal(id);
        Assert.True(await stream.MoveNextAsync());
        Assert.Equal(new[] { 3, 4 }, stream.Current.Rows.Select(x => x.Seq));
        Assert.True(stream.Current.IsTerminal);
        Assert.False(await stream.MoveNextAsync());
        Assert.Equal(new[] { 1, 2 }, source.Cursors);
    }

    [Fact]
    public async Task Subscribe_AlreadyTerminalAndFullyResumed_StillEmitsTerminal()
    {
        var source = new FakeSource { Status = "Confirmed" };
        using var hub = new SagaStreamHub(source, Options.Create(new DatabaseOptions()));
        await using var stream = hub.Subscribe(Guid.NewGuid(), 3, CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        Assert.Empty(stream.Current.Rows);
        Assert.True(stream.Current.IsTerminal);
        Assert.False(await stream.MoveNextAsync());
    }

    [Fact]
    public void IsTerminal_PaymentDeclined_WaitsForCompensation()
    {
        var snapshot = new SagaSnapshot([], "Cancelled", "PaymentDeclined", "declined");
        Assert.False(snapshot.IsTerminal);
        Assert.True((snapshot with { CompensationCompleted = true }).IsTerminal);
        Assert.True((snapshot with { FailureStage = "ReservationRejected" }).IsTerminal);
    }

    [Fact]
    public async Task Subscribe_CancelledWhileWaiting_CompletesPromptly()
    {
        using var hub = new SagaStreamHub(new FakeSource(), Options.Create(new DatabaseOptions()));
        using var cancellation = new CancellationTokenSource();
        await using var stream = hub.Subscribe(Guid.NewGuid(), 0, cancellation.Token).GetAsyncEnumerator();
        Assert.True(await stream.MoveNextAsync());
        var pending = stream.MoveNextAsync().AsTask();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private static SagaRow Row(int seq) => new(seq, "event", "orders", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

    private sealed class FakeSource : ISagaRowSource
    {
        public List<SagaRow> Rows { get; } = [];
        public List<int> Cursors { get; } = [];
        public string Status { get; set; } = "Pending";
        public Task<SagaSnapshot?> ReadAsync(Guid orderId, int afterSeq, CancellationToken cancellationToken)
        {
            Cursors.Add(afterSeq);
            return Task.FromResult<SagaSnapshot?>(new(Rows.Where(x => x.Seq > afterSeq).ToList(), Status, null, null));
        }
    }
}
