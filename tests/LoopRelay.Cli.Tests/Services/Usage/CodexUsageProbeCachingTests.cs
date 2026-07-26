using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Telemetry;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Core.Models.Repositories;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Usage;

/// <summary>
/// PERF-09. Reading the quota number spawns a whole <c>codex app-server</c> process, and the probe ran on
/// every turn to read a number that moves slowly. These tests count the process starts recorded at the
/// <c>IProcessRunner</c> seam — the same boundary a production spawn goes through — so the counts are real
/// spawns rather than a restated constant, and the TTL is driven by a fake monotonic clock so no test waits.
/// </summary>
public class CodexUsageProbeCachingTests
{
    private static Repository Repo() => new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };

    // The id:2 `account/rateLimits/read` response; the snapshot reports capacity USED, telemetry keeps REMAINING.
    private static string RateLimitsResponse(int primaryUsed, int secondaryUsed) =>
        ("""{"id":2,"result":{"rateLimits":{"primary":{"usedPercent":P_USED,"resetsAt":0},"secondary":{"usedPercent":S_USED,"resetsAt":0}}}}""")
            .Replace("P_USED", primaryUsed.ToString())
            .Replace("S_USED", secondaryUsed.ToString());

    /// <summary>A spawn that completes the exchange: the initialize response, then the rate-limits response.</summary>
    private static FakeAgentProcess AnsweringSpawn(int primaryUsed, int secondaryUsed) =>
        new(new[] { """{"id":1,"result":{}}""", RateLimitsResponse(primaryUsed, secondaryUsed) });

    /// <summary>A spawn that answers initialize and then ends the stream — the id:2 response never arrives,
    /// which is the fail-open "probe failed, capacity unknown" path.</summary>
    private static FakeAgentProcess SilentSpawn() => new(new[] { """{"id":1,"result":{}}""" });

    private static CodexUsageProbe Probe(FakeInteractiveProcessRunner runner, Func<TimeSpan> monotonicNow) =>
        new(runner, new FakeExecutableResolver(), Repo(), TimeSpan.FromSeconds(30), monotonicNow);

    [Fact]
    public async Task Query_WithinTheTtl_ServesTheCachedSnapshotWithoutSpawningAgain()
    {
        TimeSpan now = TimeSpan.Zero;
        var runner = new FakeInteractiveProcessRunner(AnsweringSpawn(75, 30));
        CodexUsageProbe probe = Probe(runner, () => now);

        CodexUsageStatus? first = await probe.QueryAsync(CancellationToken.None);
        now += CodexUsageProbe.CacheTtl - TimeSpan.FromSeconds(1);
        CodexUsageStatus? second = await probe.QueryAsync(CancellationToken.None);

        Assert.Single(runner.InteractiveCalls); // two turns, one `codex app-server` process
        Assert.Equal(25, first!.FiveHourRemainingPercent);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task Query_OnceTheTtlExpires_SpawnsAgainAndServesTheNewerSnapshot()
    {
        TimeSpan now = TimeSpan.Zero;
        FakeInteractiveProcessRunner runner =
            FakeInteractiveProcessRunner.Sequence(AnsweringSpawn(75, 30), AnsweringSpawn(90, 40));
        CodexUsageProbe probe = Probe(runner, () => now);

        CodexUsageStatus? first = await probe.QueryAsync(CancellationToken.None);
        now += CodexUsageProbe.CacheTtl + TimeSpan.FromSeconds(1);
        CodexUsageStatus? second = await probe.QueryAsync(CancellationToken.None);

        Assert.Equal(2, runner.InteractiveCalls.Count);
        Assert.Equal(25, first!.FiveHourRemainingPercent);
        Assert.Equal(10, second!.FiveHourRemainingPercent); // the stale snapshot was replaced, not re-served
    }

    [Fact]
    public async Task Query_WhenAProbeFails_ReportsUnknownWithoutCachingItOrRestartingTheTtlWindow()
    {
        // Caching the failure would be the worst of both worlds: every caller for a further full TTL would be
        // told "capacity unknown" while no probe was allowed to run and correct it.
        TimeSpan now = TimeSpan.Zero;
        FakeInteractiveProcessRunner runner =
            FakeInteractiveProcessRunner.Sequence(AnsweringSpawn(75, 30), SilentSpawn(), AnsweringSpawn(90, 40));
        CodexUsageProbe probe = Probe(runner, () => now);

        await probe.QueryAsync(CancellationToken.None);            // spawn 1: a good snapshot is cached
        now += CodexUsageProbe.CacheTtl + TimeSpan.FromSeconds(1); // it expires
        CodexUsageStatus? failed = await probe.QueryAsync(CancellationToken.None);

        // The clock does NOT move again: if the failure had refreshed the window this call would be served
        // from a cached null instead of spawning.
        CodexUsageStatus? afterFailure = await probe.QueryAsync(CancellationToken.None);

        Assert.Null(failed);
        Assert.Equal(3, runner.InteractiveCalls.Count);
        Assert.Equal(10, afterFailure!.FiveHourRemainingPercent);
    }

    [Fact]
    public async Task Query_WhenTheCallerCancelsMidProbe_CachesNothingAndLetsTheNextTurnProbe()
    {
        // A cancelled probe is intent, not a result: it must leave neither a snapshot nor a failure behind.
        TimeSpan now = TimeSpan.Zero;
        int reads = 0;
        using var cts = new CancellationTokenSource();
        var probe = new CodexUsageProbe(
            _ =>
            {
                reads++;
                if (reads > 1)
                {
                    return Task.FromResult<string?>(RateLimitsResponse(75, 30));
                }

                cts.Cancel(); // the caller stops the loop while this probe is in flight
                throw new OperationCanceledException(cts.Token);
            },
            () => now);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => probe.QueryAsync(cts.Token));
        CodexUsageStatus? next = await probe.QueryAsync(CancellationToken.None);

        Assert.Equal(2, reads);
        Assert.Equal(25, next!.FiveHourRemainingPercent);
    }
}
