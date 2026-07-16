using Xunit;

namespace LoopRelay.Certification.Tests;

public sealed class CertificationDirectTurnLifecycleTests
{
    [Fact]
    public async Task Recording_runs_only_after_the_session_is_disposed()
    {
        var session = new ObservedSession();
        bool recorded = false;

        int result = await CertificationDirectTurnLifecycle.RunAndRecordAsync(
            session,
            _ =>
            {
                Assert.False(session.Disposed);
                return Task.FromResult(42);
            },
            (value, _) =>
            {
                Assert.True(session.Disposed);
                Assert.Equal(42, value);
                recorded = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(42, result);
        Assert.True(recorded);
    }

    [Fact]
    public async Task A_failed_turn_is_disposed_without_recording()
    {
        var session = new ObservedSession();
        bool recorded = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CertificationDirectTurnLifecycle.RunAndRecordAsync<int>(
                session,
                _ => throw new InvalidOperationException("turn failed"),
                (_, _) =>
                {
                    recorded = true;
                    return Task.CompletedTask;
                },
                CancellationToken.None));

        Assert.True(session.Disposed);
        Assert.False(recorded);
    }

    private sealed class ObservedSession : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
