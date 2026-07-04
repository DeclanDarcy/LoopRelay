using System;
using System.Threading;
using System.Threading.Tasks;
using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class SessionTelemetryRecorderTests
{
    private sealed record Kit(
        SessionTelemetryRecorder Recorder, FakeCodexUsageProbe Probe, FakeCodexRolloutLocator Locator,
        FakeSessionTelemetrySink Sink, StubCostModel Cost, RecordingLoopConsole Con);

    private static Kit New()
    {
        var probe = new FakeCodexUsageProbe();
        var locator = new FakeCodexRolloutLocator();
        var sink = new FakeSessionTelemetrySink();
        var cost = new StubCostModel { MeasureValue = 42.0 };
        var con = new RecordingLoopConsole();
        var clock = new FakeClock();
        var recorder = new SessionTelemetryRecorder(probe, locator, sink, cost, clock, con);
        return new Kit(recorder, probe, locator, sink, cost, con);
    }

    private static AgentTurnResult Turn(int index = 3) =>
        new(index, AgentTurnState.Completed, "out", new AgentTokenUsage(100, 20, 30));

    [Fact]
    public async Task RecordTurn_BuildsAFullRecordFromProbePostAndTurn()
    {
        var k = New();
        k.Probe.Default = new CodexUsageStatus(58, TimeSpan.FromHours(1), 79, TimeSpan.FromHours(5)); // post
        k.Locator.Path = "/logs/rollout.jsonl";

        string? path = await k.Recorder.RecordTurnAsync(
            "myrepo", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, cachedLogPath: null, Turn(index: 3), CancellationToken.None);

        Assert.Equal("/logs/rollout.jsonl", path);
        SessionTelemetryRecord r = Assert.Single(k.Sink.Records);
        Assert.Equal("myrepo", r.RepoName);
        Assert.Equal("/logs/rollout.jsonl", r.CodexLogPath);
        Assert.Equal("Decision", r.SessionType);
        Assert.Equal(3, r.TurnIndex);
        Assert.Equal(100, r.PromptTokens);
        Assert.Equal(20, r.OutputTokens);
        Assert.Equal(30, r.CachedTokens);
        Assert.Equal(42.0, r.EffectiveTokens);
        Assert.Equal(58, r.PostFiveHourPercent);
        Assert.Equal(79, r.PostWeeklyPercent);
    }

    [Fact]
    public async Task RecordTurn_WhenPathAlreadyCached_ReusesItWithoutCallingTheLocator()
    {
        var k = New();
        k.Probe.Default = new CodexUsageStatus(50, TimeSpan.Zero, 50, TimeSpan.Zero);

        string? path = await k.Recorder.RecordTurnAsync(
            "r", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.OperationalExecution,
            DateTimeOffset.UnixEpoch, cachedLogPath: "/cached.jsonl", Turn(), CancellationToken.None);

        Assert.Equal("/cached.jsonl", path);
        Assert.Equal(0, k.Locator.Calls);
        Assert.Equal("/cached.jsonl", Assert.Single(k.Sink.Records).CodexLogPath);
    }

    [Fact]
    public async Task RecordTurn_WhenPostProbeUnavailable_WritesNullCapacities()
    {
        var k = New();
        k.Probe.Default = null; // post-probe unreadable

        await k.Recorder.RecordTurnAsync(
            "r", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, null, Turn(), CancellationToken.None);

        SessionTelemetryRecord r = Assert.Single(k.Sink.Records);
        Assert.Null(r.PostFiveHourPercent);
        Assert.Null(r.PostWeeklyPercent);
    }

    [Fact]
    public async Task RecordTurn_WhenSinkThrows_WarnsAndDoesNotThrow()
    {
        var k = New();
        k.Sink.Throw = true;
        k.Probe.Default = new CodexUsageStatus(50, TimeSpan.Zero, 50, TimeSpan.Zero);

        string? path = await k.Recorder.RecordTurnAsync(
            "r", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, "/cached.jsonl", Turn(), CancellationToken.None);

        Assert.Equal("/cached.jsonl", path); // still returns the path
        Assert.Contains(k.Con.Events, e => e.Kind == "warn");
    }

    [Fact]
    public async Task NullRecorder_ReturnsCachedPathAndRecordsNothing()
    {
        var sink = new FakeSessionTelemetrySink();
        string? path = await new NullSessionTelemetryRecorder().RecordTurnAsync(
            "r", "/w", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, "/cached", Turn(), CancellationToken.None);

        Assert.Equal("/cached", path);
        Assert.Empty(sink.Records);
    }

    [Fact]
    public async Task RecordTurn_WhenCallerCancelsDuringPostProbe_PropagatesCancellationAndWritesNothing()
    {
        var sink = new FakeSessionTelemetrySink();
        var recorder = new SessionTelemetryRecorder(
            new CancelingProbe(), new FakeCodexRolloutLocator(), sink,
            new StubCostModel(), new FakeClock(), new RecordingLoopConsole());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // A caller cancellation is intent, not a telemetry fault — it must surface, not be swallowed.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recorder.RecordTurnAsync(
            "r", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, cachedLogPath: null, Turn(), cts.Token));

        Assert.Empty(sink.Records);
    }

    /// <summary>A post-probe that honours the cancellation token (like the real CodexUsageProbe).</summary>
    private sealed class CancelingProbe : ICodexUsageProbe
    {
        public Task<CodexUsageStatus?> QueryAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<CodexUsageStatus?>(null);
        }
    }
}
