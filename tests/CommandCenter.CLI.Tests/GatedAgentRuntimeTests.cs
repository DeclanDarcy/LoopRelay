using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using CommandCenter.Core.Repositories;
using Xunit;

namespace CommandCenter.Cli.Tests;

public class GatedAgentRuntimeTests
{
    private static Repository Repo() => new() { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
    private static AgentSessionSpec Spec() => AgentSpecs.Decision(Repo());
    private static UsageLimitHit Hit() => new(TimeSpan.FromMinutes(5), null);

    private sealed record Fixture(
        GatedAgentRuntime Runtime, RecordingRuntime Inner, RecordingDetector Detector,
        RecordingSessionTelemetryRecorder Recorder, List<string> Log);

    private static Fixture New()
    {
        var log = new List<string>();
        var inner = new RecordingRuntime(log);
        var detector = new RecordingDetector(log);
        var recorder = new RecordingSessionTelemetryRecorder();
        var runtime = new GatedAgentRuntime(inner, detector, recorder, new FakeClock(), "myrepo");
        return new Fixture(runtime, inner, detector, recorder, log);
    }

    [Fact]
    public async Task RunTurn_ChecksEveryTurnResultForAUsageLimitFailure()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());

        await session.RunTurnAsync("p1");
        await session.RunTurnAsync("p2");

        // The limit can be hit by any turn of a warm session, so every result goes through the detector.
        Assert.Equal(new[] { "turn", "detect", "turn", "detect" }, f.Log);
        Assert.Equal(0, f.Detector.Waits);
    }

    [Fact]
    public async Task OpenSession_DoesNotDetectOrRecord()
    {
        var f = New();

        await f.Runtime.OpenSessionAsync(Spec());

        // Opening a session spawns the app-server but spends no quota and cannot hit the usage limit.
        Assert.Empty(f.Log);
        Assert.Empty(f.Recorder.Calls);
    }

    [Fact]
    public async Task RunTurn_ReturnsInnerResultAndForwardsThePrompt()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Inner.LastSession!.TurnResult = Turns.Completed("RESULT");

        AgentTurnResult result = await session.RunTurnAsync("hello");

        Assert.Equal("RESULT", result.Output);
        Assert.Contains("hello", f.Inner.LastSession.Prompts);
    }

    [Fact]
    public async Task RunTurn_WhenTheTurnFailsOnTheUsageLimit_WaitsAndRetriesTheSamePrompt()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Inner.LastSession!.TurnResults.Enqueue(Turns.Failed("limited", "usage limit"));
        f.Inner.LastSession.TurnResults.Enqueue(Turns.Completed("ok"));
        f.Detector.Hits.Enqueue(Hit());

        AgentTurnResult result = await session.RunTurnAsync("p");

        Assert.Equal("ok", result.Output);
        // Failed attempt is recorded, waited out, then the SAME prompt reruns on the same warm session.
        Assert.Equal(new[] { "turn", "detect", "wait", "turn", "detect" }, f.Log);
        Assert.Equal(new[] { "p", "p" }, f.Inner.LastSession.Prompts);
        Assert.Equal(2, f.Recorder.Calls.Count);
    }

    [Fact]
    public async Task RunTurn_WhenTheLimitPersists_StopsRetryingAtTheCap()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Inner.LastSession!.TurnResult = Turns.Failed("still limited", "usage limit");
        f.Detector.Default = Hit();

        AgentTurnResult result = await session.RunTurnAsync("p");

        // A persistently failing codex must eventually surface loudly instead of waiting forever — and
        // say WHY it gave up, or the operator cannot tell "capped on quota" from an unrelated crash.
        Assert.Equal(AgentTurnState.Failed, result.State);
        Assert.Equal(1 + GatedAgentSession.MaxUsageLimitRetriesPerTurn, f.Log.Count(e => e == "turn"));
        Assert.Equal(GatedAgentSession.MaxUsageLimitRetriesPerTurn, f.Detector.Waits);
        Assert.Equal(1 + GatedAgentSession.MaxUsageLimitRetriesPerTurn, f.Recorder.Calls.Count);
        Assert.Equal("exhausted", f.Log.Last());
    }

    [Fact]
    public async Task RunTurn_WhenTheFailureIsNotAUsageLimit_PropagatesWithoutRetry()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Inner.LastSession!.TurnResult = Turns.Failed("boom", "unrelated crash");

        AgentTurnResult result = await session.RunTurnAsync("p");

        Assert.Equal(AgentTurnState.Failed, result.State);
        Assert.Equal(new[] { "turn", "detect" }, f.Log);
        Assert.Equal(0, f.Detector.Waits);
    }

    [Fact]
    public async Task RunTurn_WhenTheCodexProcessDied_DoesNotRetry()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Inner.LastSession!.TurnResults.Enqueue(Turns.Failed("limited", "usage limit"));
        f.Detector.Hits.Enqueue(Hit());
        f.Inner.LastSession.State = AgentProcessState.Exited;

        AgentTurnResult result = await session.RunTurnAsync("p");

        // A dead app-server cannot run another turn — a retry would surface as a spurious
        // OperationCanceledException that LoopRunner misreads as user cancellation. Fail loudly instead.
        Assert.Equal(AgentTurnState.Failed, result.State);
        Assert.Equal(new[] { "turn", "detect" }, f.Log);
        Assert.Equal(0, f.Detector.Waits);
    }

    [Fact]
    public async Task RunTurn_WhenTheCodexProcessDiesDuringTheWait_DoesNotRetry()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        RecordingSession liveSession = f.Inner.LastSession!;
        liveSession.TurnResults.Enqueue(Turns.Failed("limited", "usage limit"));
        f.Detector.Hits.Enqueue(Hit());
        f.Detector.OnWait = () => liveSession.State = AgentProcessState.Exited;

        AgentTurnResult result = await session.RunTurnAsync("p");

        // The wait can span days; the process may die in the meantime, so liveness is re-checked after it.
        Assert.Equal(AgentTurnState.Failed, result.State);
        Assert.Equal(new[] { "turn", "detect", "wait" }, f.Log);
        Assert.Equal(1, f.Log.Count(e => e == "turn"));
    }

    [Fact]
    public async Task RunTurn_WhenTheWaitIsCancelled_PropagatesAfterRecordingTheFailedAttempt()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Inner.LastSession!.TurnResult = Turns.Failed("limited", "usage limit");
        f.Detector.Default = Hit();
        f.Detector.ThrowOnWait = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.RunTurnAsync("p"));

        Assert.Single(f.Recorder.Calls);
    }

    [Fact]
    public async Task CloseSession_UnwrapsAndPassesTheInnerSessionToTheInnerRuntime()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());

        await f.Runtime.CloseSessionAsync(session);

        // The inner runtime keys teardown off the session it created, so it must receive the RAW inner
        // session, never the gated wrapper.
        Assert.Same(f.Inner.LastSession, f.Inner.ClosedSession);
    }

    [Fact]
    public async Task GatedSession_ExposesInnerIdentity()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());

        Assert.Equal(f.Inner.LastSession!.RepositoryId, session.RepositoryId);
        Assert.Equal(f.Inner.LastSession.SessionId, session.SessionId);
        Assert.Equal(f.Inner.LastSession.Role, session.Role);
    }

    [Fact]
    public async Task Dispose_DelegatesToInnerSession()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());

        await session.DisposeAsync();

        Assert.True(f.Inner.LastSession!.Disposed);
    }

    [Fact]
    public async Task RunTurn_EmitsOneRecordPerTurn()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Inner.LastSession!.TurnResult = new AgentTurnResult(1, AgentTurnState.Completed, "o", new AgentTokenUsage(1, 1));

        await session.RunTurnAsync("p1");

        var call = Assert.Single(f.Recorder.Calls);
        Assert.Equal(SessionRole.Decision, call.Role);
        Assert.Equal(1, call.TurnIndex);
        Assert.Null(call.CachedLogPath);                 // first turn: nothing cached yet
    }

    [Fact]
    public async Task RunTurn_SecondTurnReusesTheCachedLogPathFromTheFirst()
    {
        var f = New();
        f.Recorder.PathToReturn = "/rollout.jsonl";
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());

        await session.RunTurnAsync("p1");
        await session.RunTurnAsync("p2");

        Assert.Equal(2, f.Recorder.Calls.Count);
        Assert.Null(f.Recorder.Calls[0].CachedLogPath);
        Assert.Equal("/rollout.jsonl", f.Recorder.Calls[1].CachedLogPath);
    }

    [Fact]
    public async Task RunOneShot_EmitsOneRecordAndReturnsTheResult()
    {
        var f = New();

        AgentTurnResult result = await f.Runtime.RunOneShotAsync(Spec(), "prompt");

        Assert.Equal("oneshot", result.Output);
        Assert.Equal(new[] { "oneshot" }, f.Log);
        Assert.Single(f.Recorder.Calls);
    }

    [Fact]
    public async Task RunOneShot_WhenItFailsOnTheUsageLimit_DoesNotRetry()
    {
        var f = New();
        f.Inner.OneShotResult = Turns.Failed("limited", "usage limit");
        f.Detector.Default = Hit();

        AgentTurnResult result = await f.Runtime.RunOneShotAsync(Spec(), "prompt");

        // One-shots run in caller-seeded sandboxes where the output often overwrites a seeded input; a
        // failed attempt may have half-written it, so a blind rerun could read its predecessor's partial
        // output as authoritative. Propagate instead — the caller owns re-seeding.
        Assert.Equal(AgentTurnState.Failed, result.State);
        Assert.Equal(new[] { "oneshot" }, f.Log);
        Assert.Equal(0, f.Detector.Waits);
        Assert.Single(f.Recorder.Calls);
    }

    [Fact]
    public void GatedSessionExposesTheInnerThreadId()
    {
        var log = new List<string>();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var gated = new GatedAgentSession(
            new RecordingSession(log, AgentSpecs.Decision(repo)),
            new RecordingDetector(log),
            new NullSessionTelemetryRecorder(),
            "r", "/repo", DateTimeOffset.UtcNow);

        // Persist-after-turn reads the thread id THROUGH the gate — a missing passthrough would silently
        // disable resume in production (the gated wrapper is what DecisionSession actually holds).
        Assert.Equal("thread-inner", gated.ThreadId);
    }

    // --- local recording fakes ---

    private sealed class RecordingDetector(List<string> log) : IUsageLimitDetector
    {
        public Queue<UsageLimitHit?> Hits { get; } = new();
        public UsageLimitHit? Default { get; set; }
        public Exception? ThrowOnWait { get; set; }
        public Action? OnWait { get; set; }
        public int Waits { get; private set; }

        public UsageLimitHit? Detect(AgentTurnResult result)
        {
            log.Add("detect");
            return Hits.Count > 0 ? Hits.Dequeue() : Default;
        }

        public Task WaitOutAsync(UsageLimitHit hit, CancellationToken cancellationToken)
        {
            Waits++;
            log.Add("wait");
            if (ThrowOnWait is not null)
            {
                throw ThrowOnWait;
            }

            OnWait?.Invoke();
            return Task.CompletedTask;
        }

        public void WarnRetriesExhausted(int retries) => log.Add("exhausted");
    }

    private sealed class RecordingSessionTelemetryRecorder : ISessionTelemetryRecorder
    {
        public List<(SessionRole Role, int TurnIndex, string? CachedLogPath)> Calls { get; } = new();
        public string? PathToReturn { get; set; } = "/log";

        public Task<string?> RecordTurnAsync(
            string repoName, string workingDirectory, SessionIdentity sessionId, SessionRole role,
            DateTimeOffset openedAtUtc, string? cachedLogPath, AgentTurnResult result,
            CancellationToken cancellationToken)
        {
            Calls.Add((role, result.TurnIndex, cachedLogPath));
            return Task.FromResult(PathToReturn);
        }
    }

    private sealed class RecordingRuntime(List<string> log) : IAgentRuntime
    {
        public RecordingSession? LastSession { get; private set; }
        public IAgentSession? ClosedSession { get; private set; }
        public AgentTurnResult OneShotResult { get; set; } = Turns.Completed("oneshot");

        public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken cancellationToken = default)
        {
            LastSession = new RecordingSession(log, spec);
            return Task.FromResult<IAgentSession>(LastSession);
        }

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec, string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken cancellationToken = default)
        {
            log.Add("oneshot");
            return Task.FromResult(OneShotResult);
        }

        public ValueTask CloseSessionAsync(IAgentSession session)
        {
            ClosedSession = session;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingSession(List<string> log, AgentSessionSpec spec) : IAgentSession
    {
        public List<string> Prompts { get; } = new();
        public Queue<AgentTurnResult> TurnResults { get; } = new();
        public AgentTurnResult TurnResult { get; set; } = Turns.Completed("turn");
        public bool Disposed { get; private set; }

        public SessionIdentity SessionId => spec.SessionId;
        public string RepositoryId => spec.RepositoryId;
        public SessionRole Role => spec.Role;
        public AgentSessionMode Mode => AgentSessionMode.Persistent;
        public AgentProcessState State { get; set; } = AgentProcessState.Running;
        public int CompletedTurns => 0;
        public AgentTokenUsage TotalUsage => new(0, 0);
        public string? ThreadId => "thread-inner";

        public Task<AgentTurnResult> RunTurnAsync(
            string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken cancellationToken = default)
        {
            log.Add("turn");
            Prompts.Add(prompt);
            return Task.FromResult(TurnResults.Count > 0 ? TurnResults.Dequeue() : TurnResult);
        }

        public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
