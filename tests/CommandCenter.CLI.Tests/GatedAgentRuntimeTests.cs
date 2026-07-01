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

    private sealed record Fixture(GatedAgentRuntime Runtime, RecordingRuntime Inner, RecordingGate Gate, List<string> Log);

    private static Fixture New()
    {
        var log = new List<string>();
        var inner = new RecordingRuntime(log);
        var gate = new RecordingGate(log);
        return new Fixture(new GatedAgentRuntime(inner, gate), inner, gate, log);
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

    // --- local recording fakes ---

    private sealed class RecordingGate(List<string> log) : IUsageGate
    {
        public int Calls { get; private set; }
        public Exception? Throw { get; set; }

        public Task WaitForCapacityAsync(CancellationToken cancellationToken)
        {
            Calls++;
            log.Add("gate");
            return Throw is not null ? throw Throw : Task.CompletedTask;
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
