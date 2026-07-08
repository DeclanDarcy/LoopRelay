using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services;
using LoopRelay.Infrastructure.Models.Diagnostics;
using Xunit;

namespace LoopRelay.Cli.Tests.Services;

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

    private static InputWaitObservation Observation(SessionIdentity sessionId) =>
        new(
            "repo-id",
            sessionId,
            SessionRole.Decision,
            3,
            "app-server",
            Model: null,
            PromptChars: 12,
            PromptBytes: 12,
            PromptTokensEstimated: 3,
            TokenEstimateSource: "DeterministicAgentTokenEstimator",
            PromptPreparedAt: new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero),
            RequestWriteStartedAt: new DateTimeOffset(2026, 7, 1, 12, 0, 1, TimeSpan.Zero),
            RequestSubmittedAt: new DateTimeOffset(2026, 7, 1, 12, 0, 2, TimeSpan.Zero),
            RequestAcceptedAt: new DateTimeOffset(2026, 7, 1, 12, 0, 3, TimeSpan.Zero),
            FirstProtocolEventAt: new DateTimeOffset(2026, 7, 1, 12, 0, 4, TimeSpan.Zero),
            FirstOutputAt: new DateTimeOffset(2026, 7, 1, 12, 0, 5, TimeSpan.Zero),
            CompletedAt: new DateTimeOffset(2026, 7, 1, 12, 0, 6, TimeSpan.Zero),
            Status: "Completed",
            EstimatorVersion: "DeterministicAgentTokenEstimator:v1");

    [Fact]
    public async Task RecordTurn_BuildsAFullRecordFromProbePostAndTurn()
    {
        var k = New();
        k.Probe.Default = new CodexUsageStatus(58, TimeSpan.FromHours(1), 79, TimeSpan.FromHours(5)); // post
        k.Locator.Path = "/logs/rollout.jsonl";

        string? path = await k.Recorder.RecordTurnAsync(
            "myrepo", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, cachedLogPath: null, result: Turn(index: 3),
            inputWait: null, CancellationToken.None);

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
            DateTimeOffset.UnixEpoch, cachedLogPath: "/cached.jsonl", result: Turn(),
            inputWait: null, CancellationToken.None);

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
            DateTimeOffset.UnixEpoch, null, Turn(), inputWait: null, CancellationToken.None);

        SessionTelemetryRecord r = Assert.Single(k.Sink.Records);
        Assert.Null(r.PostFiveHourPercent);
        Assert.Null(r.PostWeeklyPercent);
    }

    [Fact]
    public async Task RecordTurn_AddsInputWaitObservationFields()
    {
        var k = New();
        var sessionId = new SessionIdentity(Guid.NewGuid());

        await k.Recorder.RecordTurnAsync(
            "r", "/work", sessionId, SessionRole.Decision,
            DateTimeOffset.UnixEpoch, null, Turn(index: 3), Observation(sessionId), CancellationToken.None);

        SessionTelemetryRecord r = Assert.Single(k.Sink.Records);
        Assert.Equal("app-server", r.Transport);
        Assert.Null(r.Model);
        Assert.Equal(12, r.PromptChars);
        Assert.Equal(12, r.PromptBytes);
        Assert.Equal(3, r.PromptTokensEstimated);
        Assert.Equal("DeterministicAgentTokenEstimator", r.TokenEstimateSource);
        Assert.NotNull(r.FirstProtocolEventAt);
        Assert.NotNull(r.FirstOutputAt);
        Assert.Equal(100, r.ReportedPromptTokens);
        Assert.Equal(30, r.ReportedCachedTokens);
        Assert.Equal(20, r.ReportedOutputTokens);
        Assert.Equal("Completed", r.InputWaitStatus);
        Assert.Equal("DeterministicAgentTokenEstimator:v1", r.EstimatorVersion);
    }

    [Fact]
    public async Task RecordTurn_WhenSinkThrows_WarnsAndDoesNotThrow()
    {
        var k = New();
        k.Sink.Throw = true;
        k.Probe.Default = new CodexUsageStatus(50, TimeSpan.Zero, 50, TimeSpan.Zero);

        string? path = await k.Recorder.RecordTurnAsync(
            "r", "/work", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, "/cached.jsonl", Turn(), inputWait: null, CancellationToken.None);

        Assert.Equal("/cached.jsonl", path); // still returns the path
        Assert.Contains(k.Con.Events, e => e.Kind == "warn");
    }

    [Fact]
    public async Task NullRecorder_ReturnsCachedPathAndRecordsNothing()
    {
        var sink = new FakeSessionTelemetrySink();
        string? path = await new NullSessionTelemetryRecorder().RecordTurnAsync(
            "r", "/w", new SessionIdentity(Guid.NewGuid()), SessionRole.Decision,
            DateTimeOffset.UnixEpoch, "/cached", Turn(), inputWait: null, CancellationToken.None);

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
            DateTimeOffset.UnixEpoch, cachedLogPath: null, result: Turn(), inputWait: null, cts.Token));

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
