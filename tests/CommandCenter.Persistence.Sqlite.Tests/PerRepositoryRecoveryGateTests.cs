using CommandCenter.Persistence.Sqlite;

namespace CommandCenter.Persistence.Sqlite.Tests;

/// <summary>
/// Fix C (refactor-lazy-sqlite.md): <see cref="PerRepositoryRecoveryGate"/> is a per-repo SERIALIZER, not a
/// once-per-process cache. These tests pin the two invariants the live-read endpoints depend on:
///   (1) concurrent same-repo calls SERIALIZE (the underlying recovery services have no internal lock, so two
///       racing first-requests must not run the operation concurrently and tear a snapshot); and
///   (2) every call RE-RUNS and returns the FRESH result of THIS invocation — a second GET /workflow/history (or
///       /recovery) must reflect current state, never a cached first result.
/// </summary>
public sealed class PerRepositoryRecoveryGateTests
{
    [Fact]
    public async Task ConcurrentSameRepoCallsSerialize()
    {
        var gate = new PerRepositoryRecoveryGate();
        Guid repository = Guid.NewGuid();
        int concurrent = 0;
        int observedMaxConcurrency = 0;
        int completed = 0;

        async Task<int> Operation(CancellationToken ct)
        {
            int current = Interlocked.Increment(ref concurrent);
            observedMaxConcurrency = Math.Max(observedMaxConcurrency, current);
            // Hold the critical section long enough that an unserialized racer would overlap and be observed.
            await Task.Delay(25, ct);
            Interlocked.Decrement(ref concurrent);
            return Interlocked.Increment(ref completed);
        }

        Task<int>[] calls = Enumerable.Range(0, 8)
            .Select(_ => gate.RunAsync("scope", repository, Operation, CancellationToken.None))
            .ToArray();
        int[] results = await Task.WhenAll(calls);

        // Serialized: the operation never ran two-at-a-time for the same repo.
        Assert.Equal(1, observedMaxConcurrency);
        // Every call actually RAN (re-run, not coalesced/cached) — 8 distinct completion ordinals.
        Assert.Equal(8, results.Distinct().Count());
        Assert.Equal(8, completed);
    }

    [Fact]
    public async Task DifferentReposRunConcurrently()
    {
        var gate = new PerRepositoryRecoveryGate();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Each op signals entry then waits for the OTHER repo's op to enter. If the gate (wrongly) serialized
        // across repositories, the second op could never enter while the first waits, and this would deadlock /
        // time out. Both calls are started independently so they genuinely overlap.
        Task first = gate.RunAsync("scope", Guid.NewGuid(), async ct =>
        {
            firstEntered.SetResult();
            await secondEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }, CancellationToken.None);
        Task second = gate.RunAsync("scope", Guid.NewGuid(), async ct =>
        {
            secondEntered.SetResult();
            await firstEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), ct);
        }, CancellationToken.None);

        await Task.WhenAll(first, second);
    }

    [Fact]
    public async Task EachCallReRunsAndReturnsFreshResult()
    {
        // The gate must NOT cache the first result. Live reads mutate observable state between calls; the second
        // call must reflect it. We model "current state" with a counter incremented inside the operation.
        var gate = new PerRepositoryRecoveryGate();
        Guid repository = Guid.NewGuid();
        int state = 0;

        int first = await gate.RunAsync(
            "scope", repository, ct => Task.FromResult(Interlocked.Increment(ref state)), CancellationToken.None);
        int second = await gate.RunAsync(
            "scope", repository, ct => Task.FromResult(Interlocked.Increment(ref state)), CancellationToken.None);

        Assert.Equal(1, first);
        Assert.Equal(2, second); // fresh re-run, NOT the cached first result.
    }
}
