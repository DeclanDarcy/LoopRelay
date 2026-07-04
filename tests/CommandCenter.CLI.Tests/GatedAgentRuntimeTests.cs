using System;
using System.Collections.Generic;
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

    private sealed record Fixture(
        GatedAgentRuntime Runtime, RecordingRuntime Inner, RecordingGate Gate,
        RecordingSessionTelemetryRecorder Recorder, List<string> Log);

    private static Fixture New()
    {
        var log = new List<string>();
        var inner = new RecordingRuntime(log);
        var gate = new RecordingGate(log);
        var recorder = new RecordingSessionTelemetryRecorder();
        var runtime = new GatedAgentRuntime(inner, gate, recorder, new FakeClock(), "myrepo");
        return new Fixture(runtime, inner, gate, recorder, log);
    }

    [Fact]
    public async Task RunTurn_RunsTheGateBeforeEveryTurn()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());

        await session.RunTurnAsync("p1");
        await session.RunTurnAsync("p2");

        // Each turn is preceded by a gate check — quota can drain between turns of the same warm session.
        Assert.Equal(new[] { "gate", "turn", "gate", "turn" }, f.Log);
        Assert.Equal(2, f.Gate.Calls);
    }

    [Fact]
    public async Task OpenSession_DoesNotRunTheGate()
    {
        var f = New();

        await f.Runtime.OpenSessionAsync(Spec());

        // Opening a session spawns the app-server but spends no quota, so it must not gate (or block).
        Assert.Equal(0, f.Gate.Calls);
        Assert.DoesNotContain("gate", f.Log);
    }

    [Fact]
    public async Task RunOneShot_RunsTheGateBeforeDelegating()
    {
        var f = New();

        await f.Runtime.RunOneShotAsync(Spec(), "prompt");

        Assert.Equal(new[] { "gate", "oneshot" }, f.Log);
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
    public async Task RunTurn_WhenGateThrowsCancellation_DoesNotRunTheTurn()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Gate.Throw = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.RunTurnAsync("p"));

        Assert.DoesNotContain("turn", f.Log);
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
    public async Task RunTurn_EmitsOneRecordPerTurnWithTheGatesPreStatus()
    {
        var f = New();
        f.Gate.Status = new CodexUsageStatus(70, TimeSpan.FromHours(1), 90, TimeSpan.FromHours(5));
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Inner.LastSession!.TurnResult = new AgentTurnResult(1, AgentTurnState.Completed, "o", new AgentTokenUsage(1, 1));

        await session.RunTurnAsync("p1");

        var call = Assert.Single(f.Recorder.Calls);
        Assert.Equal(SessionRole.Decision, call.Role);
        Assert.Equal(1, call.TurnIndex);
        Assert.Null(call.CachedLogPath);                 // first turn: nothing cached yet
        Assert.Equal(70, call.Pre!.FiveHourRemainingPercent);
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
    public async Task RunOneShot_EmitsOneRecord()
    {
        var f = New();

        await f.Runtime.RunOneShotAsync(Spec(), "prompt");

        Assert.Single(f.Recorder.Calls);
    }

    [Fact]
    public async Task RunTurn_WhenGateThrows_EmitsNoRecord()
    {
        var f = New();
        IAgentSession session = await f.Runtime.OpenSessionAsync(Spec());
        f.Gate.Throw = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.RunTurnAsync("p"));

        Assert.Empty(f.Recorder.Calls);
    }

    [Fact]
    public void GatedSessionExposesTheInnerThreadId()
    {
        var log = new List<string>();
        var repo = new Repository { Id = Guid.NewGuid(), Name = "r", Path = "/repo" };
        var gated = new GatedAgentSession(
            new RecordingSession(log, AgentSpecs.Decision(repo)),
            new RecordingGate(log),
            new NullSessionTelemetryRecorder(),
            "r", "/repo", DateTimeOffset.UtcNow);

        // Persist-after-turn reads the thread id THROUGH the gate — a missing passthrough would silently
        // disable resume in production (the gated wrapper is what DecisionSession actually holds).
        Assert.Equal("thread-inner", gated.ThreadId);
    }

    // --- local recording fakes ---

    private sealed class RecordingGate(List<string> log) : IUsageGate
    {
        public int Calls { get; private set; }
        public Exception? Throw { get; set; }
        public CodexUsageStatus? Status { get; set; }

        public Task<CodexUsageStatus?> WaitForCapacityAsync(CancellationToken cancellationToken)
        {
            Calls++;
            log.Add("gate");
            return Throw is not null ? throw Throw : Task.FromResult(Status);
        }
    }

    private sealed class RecordingSessionTelemetryRecorder : ISessionTelemetryRecorder
    {
        public List<(SessionRole Role, int TurnIndex, string? CachedLogPath, CommandCenter.Cli.CodexUsageStatus? Pre)> Calls { get; } = new();
        public string? PathToReturn { get; set; } = "/log";

        public Task<string?> RecordTurnAsync(
            string repoName, string workingDirectory, SessionIdentity sessionId, SessionRole role,
            DateTimeOffset openedAtUtc, string? cachedLogPath, AgentTurnResult result,
            CodexUsageStatus? preStatus, CancellationToken cancellationToken)
        {
            Calls.Add((role, result.TurnIndex, cachedLogPath, preStatus));
            return Task.FromResult(PathToReturn);
        }
    }

    private sealed class RecordingRuntime(List<string> log) : IAgentRuntime
    {
        public RecordingSession? LastSession { get; private set; }
        public IAgentSession? ClosedSession { get; private set; }

        public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken cancellationToken = default)
        {
            LastSession = new RecordingSession(log, spec);
            return Task.FromResult<IAgentSession>(LastSession);
        }

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec, string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken cancellationToken = default)
        {
            log.Add("oneshot");
            return Task.FromResult(Turns.Completed("oneshot"));
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
        public AgentTurnResult TurnResult { get; set; } = Turns.Completed("turn");
        public bool Disposed { get; private set; }

        public SessionIdentity SessionId => spec.SessionId;
        public string RepositoryId => spec.RepositoryId;
        public SessionRole Role => spec.Role;
        public AgentSessionMode Mode => AgentSessionMode.Persistent;
        public AgentProcessState State => AgentProcessState.Running;
        public int CompletedTurns => 0;
        public AgentTokenUsage TotalUsage => new(0, 0);
        public string? ThreadId => "thread-inner";

        public Task<AgentTurnResult> RunTurnAsync(
            string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken cancellationToken = default)
        {
            log.Add("turn");
            Prompts.Add(prompt);
            return Task.FromResult(TurnResult);
        }

        public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
