using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace LoopRelay.Orchestration.Streaming;

/// <summary>
/// In-memory single-producer / multi-subscriber broadcast for one orchestrator stream
/// (planning, execution, or decision). Each subscriber drains its own unbounded channel; a
/// bounded replay buffer lets a reconnecting client resume after a <c>Last-Event-ID</c> without
/// re-receiving frames it already saw. Purely transient: it lives only for the orchestrator's
/// lifetime and is <see cref="Complete"/>d on disposal, ending every open subscription.
/// </summary>
public sealed class OrchestratorStreamChannel
{
    private readonly object gate = new();
    private readonly List<Channel<OrchestratorStreamEvent>> subscribers = new();
    private readonly Queue<OrchestratorStreamEvent> replay = new();
    private readonly int replayCapacity;
    private long sequence;
    private bool completed;

    public OrchestratorStreamChannel(int replayCapacity = 256)
    {
        if (replayCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replayCapacity));
        }

        this.replayCapacity = replayCapacity;
    }

    public long LastSequence
    {
        get
        {
            lock (gate)
            {
                return sequence;
            }
        }
    }

    public bool IsCompleted
    {
        get
        {
            lock (gate)
            {
                return completed;
            }
        }
    }

    /// <summary>Publishes a frame to every live subscriber and the replay buffer. Returns the stamped frame.</summary>
    public OrchestratorStreamEvent Publish(string type, string data)
    {
        lock (gate)
        {
            if (completed)
            {
                throw new InvalidOperationException("Stream channel is completed.");
            }

            var stamped = new OrchestratorStreamEvent(++sequence, type, data);

            replay.Enqueue(stamped);
            while (replay.Count > replayCapacity)
            {
                replay.Dequeue();
            }

            // TryWrite always succeeds for an unbounded channel; a completed (disposed) subscriber
            // channel simply rejects the write and is reaped by its own SubscribeAsync finally block.
            foreach (Channel<OrchestratorStreamEvent> subscriber in subscribers)
            {
                subscriber.Writer.TryWrite(stamped);
            }

            return stamped;
        }
    }

    /// <summary>
    /// Subscribes to the stream, first replaying buffered frames with sequence greater than
    /// <paramref name="afterSequence"/>, then yielding live frames until cancellation or completion.
    /// The replay snapshot and subscription registration happen atomically, so each frame is
    /// delivered exactly once (no gap, no duplicate) across the replay/live boundary.
    /// </summary>
    public async IAsyncEnumerable<OrchestratorStreamEvent> SubscribeAsync(
        long afterSequence = 0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Channel<OrchestratorStreamEvent> channel = Channel.CreateUnbounded<OrchestratorStreamEvent>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        OrchestratorStreamEvent[] missed;
        bool live;
        lock (gate)
        {
            missed = replay.Where(frame => frame.Sequence > afterSequence).ToArray();
            live = !completed;
            if (live)
            {
                subscribers.Add(channel);
            }
        }

        try
        {
            foreach (OrchestratorStreamEvent frame in missed)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return frame;
            }

            if (!live)
            {
                yield break;
            }

            await foreach (OrchestratorStreamEvent frame in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return frame;
            }
        }
        finally
        {
            lock (gate)
            {
                subscribers.Remove(channel);
            }

            channel.Writer.TryComplete();
        }
    }

    /// <summary>Ends the stream: completes every live subscriber so their enumerations finish.</summary>
    public void Complete()
    {
        lock (gate)
        {
            if (completed)
            {
                return;
            }

            completed = true;
            foreach (Channel<OrchestratorStreamEvent> subscriber in subscribers)
            {
                subscriber.Writer.TryComplete();
            }

            subscribers.Clear();
        }
    }
}
