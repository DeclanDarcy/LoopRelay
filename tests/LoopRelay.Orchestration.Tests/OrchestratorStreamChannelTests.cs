using LoopRelay.Orchestration.Streaming;

namespace LoopRelay.Orchestration.Tests;

public sealed class OrchestratorStreamChannelTests
{
    private static CancellationToken Timeout() => new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;

    [Fact]
    public void Publish_stamps_monotonically_increasing_sequences()
    {
        var channel = new OrchestratorStreamChannel();

        OrchestratorStreamEvent first = channel.Publish("planning", "a");
        OrchestratorStreamEvent second = channel.Publish("planning", "b");

        Assert.Equal(1, first.Sequence);
        Assert.Equal(2, second.Sequence);
        Assert.Equal(2, channel.LastSequence);
    }

    [Fact]
    public async Task A_live_subscriber_receives_frames_published_after_it_subscribes()
    {
        var channel = new OrchestratorStreamChannel();
        await using IAsyncEnumerator<OrchestratorStreamEvent> subscriber =
            channel.SubscribeAsync(cancellationToken: Timeout()).GetAsyncEnumerator();

        // Begin the move first so the subscriber is registered before we publish.
        ValueTask<bool> pending = subscriber.MoveNextAsync();
        channel.Publish("planning", "live");

        Assert.True(await pending);
        Assert.Equal("live", subscriber.Current.Data);
    }

    [Fact]
    public async Task A_reconnecting_subscriber_replays_only_frames_after_its_last_event_id()
    {
        var channel = new OrchestratorStreamChannel();
        channel.Publish("planning", "one");   // seq 1
        channel.Publish("planning", "two");   // seq 2
        channel.Publish("planning", "three"); // seq 3

        var replayed = new List<OrchestratorStreamEvent>();
        await using IAsyncEnumerator<OrchestratorStreamEvent> subscriber =
            channel.SubscribeAsync(afterSequence: 1, cancellationToken: Timeout()).GetAsyncEnumerator();

        // Two buffered frames (seq 2, 3) replay synchronously before any live wait.
        Assert.True(await subscriber.MoveNextAsync());
        replayed.Add(subscriber.Current);
        Assert.True(await subscriber.MoveNextAsync());
        replayed.Add(subscriber.Current);

        Assert.Equal(new[] { 2L, 3L }, replayed.Select(frame => frame.Sequence));
        Assert.Equal(new[] { "two", "three" }, replayed.Select(frame => frame.Data));
    }

    [Fact]
    public async Task Completing_the_channel_ends_open_subscriptions()
    {
        var channel = new OrchestratorStreamChannel();
        await using IAsyncEnumerator<OrchestratorStreamEvent> subscriber =
            channel.SubscribeAsync(cancellationToken: Timeout()).GetAsyncEnumerator();

        ValueTask<bool> pending = subscriber.MoveNextAsync();
        channel.Publish("planning", "last");
        Assert.True(await pending);
        Assert.Equal("last", subscriber.Current.Data);

        channel.Complete();

        Assert.False(await subscriber.MoveNextAsync());
        Assert.True(channel.IsCompleted);
    }

    [Fact]
    public void Publishing_to_a_completed_channel_throws()
    {
        var channel = new OrchestratorStreamChannel();
        channel.Complete();

        Assert.Throws<InvalidOperationException>(() => channel.Publish("planning", "x"));
    }

    [Fact]
    public async Task Subscribing_to_a_completed_channel_replays_buffer_then_ends()
    {
        var channel = new OrchestratorStreamChannel();
        channel.Publish("planning", "buffered");
        channel.Complete();

        var seen = new List<string>();
        await foreach (OrchestratorStreamEvent frame in channel.SubscribeAsync(cancellationToken: Timeout()))
        {
            seen.Add(frame.Data);
        }

        Assert.Equal(new[] { "buffered" }, seen);
    }
}
