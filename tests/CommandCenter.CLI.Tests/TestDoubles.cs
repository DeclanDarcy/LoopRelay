using System.Collections.Concurrent;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;

namespace CommandCenter.Cli.Tests;

internal sealed class RecordingLoopConsole : ILoopConsole
{
    public ConcurrentQueue<(string Kind, string Text)> Events { get; } = new();
    public void Phase(string phase) => Events.Enqueue(("phase", phase));
    public void Message(string content) => Events.Enqueue(("message", content));
    public void Delta(string text) => Events.Enqueue(("delta", text));
    public void Info(string text) => Events.Enqueue(("info", text));
    public void Warn(string text) => Events.Enqueue(("warn", text));
    public void Error(string text) => Events.Enqueue(("error", text));

    public IReadOnlyList<string> Messages =>
        Events.Where(e => e.Kind == "message").Select(e => e.Text).ToList();
}

/// <summary>A scripted codex turn: inspect (spec, prompt), optionally mutate the store, return a result.</summary>
internal sealed record ScriptedTurn(Func<AgentSessionSpec, string, IArtifactStore, AgentTurnResult> Handler);

internal static class Turns
{
    public static AgentTurnResult Completed(string output) =>
        new(0, AgentTurnState.Completed, output, new AgentTokenUsage(0, 0));

    public static AgentTurnResult Failed(string output = "boom") =>
        new(0, AgentTurnState.Failed, output, new AgentTokenUsage(0, 0));
}

internal sealed class FakeAgentRuntime(IArtifactStore store) : IAgentRuntime
{
    public Queue<ScriptedTurn> OneShotTurns { get; } = new();
    public Queue<ScriptedTurn> SessionTurns { get; } = new();
    public int OpenSessions { get; private set; }
    public int ClosedSessions { get; private set; }
    public List<(AgentSessionSpec Spec, string Prompt)> OneShotCalls { get; } = new();

    public Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec, string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken ct = default)
    {
        OneShotCalls.Add((spec, prompt));
        ScriptedTurn turn = OneShotTurns.Dequeue();
        return Task.FromResult(turn.Handler(spec, prompt, store));
    }

    public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken ct = default)
    {
        OpenSessions++;
        return Task.FromResult<IAgentSession>(new FakeAgentSession(this, spec, store));
    }

    public ValueTask CloseSessionAsync(IAgentSession session)
    {
        ClosedSessions++;
        return ValueTask.CompletedTask;
    }

    internal AgentTurnResult RunSessionTurn(AgentSessionSpec spec, string prompt) =>
        SessionTurns.Dequeue().Handler(spec, prompt, store);
}

internal sealed class FakeAgentSession(FakeAgentRuntime runtime, AgentSessionSpec spec, IArtifactStore store) : IAgentSession
{
    public SessionIdentity SessionId => spec.SessionId;
    public string RepositoryId => spec.RepositoryId;
    public SessionRole Role => spec.Role;
    public AgentSessionMode Mode => AgentSessionMode.Persistent;
    public AgentProcessState State => AgentProcessState.Running;
    public int CompletedTurns => 0;
    public AgentTokenUsage TotalUsage => new(0, 0);

    public Task<AgentTurnResult> RunTurnAsync(
        string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken ct = default) =>
        Task.FromResult(runtime.RunSessionTurn(spec, prompt));

    public Task CancelAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
