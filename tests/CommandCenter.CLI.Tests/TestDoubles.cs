using System.Collections.Concurrent;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Cli;
using CommandCenter.Core.Artifacts;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;

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

/// <summary>Cost model with directly-controlled scalars so a CLI test can drive the economic marginal rule.</summary>
internal sealed class StubCostModel : IDecisionCostModel
{
    public double MeasureValue { get; set; }
    public double EstimateValue { get; set; }
    public double Measure(AgentTokenUsage turn) => MeasureValue;
    public double EstimateNextCycle(DecisionCostForecast forecast) => EstimateValue;
}

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
        return Task.FromResult<IAgentSession>(new FakeAgentSession(this, spec));
    }

    public ValueTask CloseSessionAsync(IAgentSession session)
    {
        ClosedSessions++;
        return ValueTask.CompletedTask;
    }

    internal AgentTurnResult RunSessionTurn(AgentSessionSpec spec, string prompt) =>
        SessionTurns.Dequeue().Handler(spec, prompt, store);
}

/// <summary>
/// Scripts <see cref="IProcessRunner.RunAsync"/> for the CommitGate (no real git). Records every call,
/// and returns whatever <see cref="Handler"/> produces (or a zero-exit success when Handler is null).
/// StartAsync/StartInteractiveAsync throw — the gate only ever runs RunAsync.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Args)> Calls { get; } = new();

    public Func<IReadOnlyList<string>, ProcessRunResult>? Handler { get; set; }

    public Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        Calls.Add((fileName, arguments));
        return Task.FromResult(Handler?.Invoke(arguments) ?? Ok());
    }

    public Task<ProcessStartResult> StartAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string? standardInput = null,
        Func<string, Task>? onStandardOutput = null,
        Func<string, Task>? onStandardError = null,
        Func<int?, Task>? onExit = null) =>
        throw new NotSupportedException();

    public Task<IAgentProcess> StartInteractiveAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public static ProcessRunResult Ok(string stdout = "") =>
        new() { ExitCode = 0, StandardOutput = stdout, StandardError = string.Empty, Duration = TimeSpan.Zero };

    public static ProcessRunResult Fail(string stderr) =>
        new() { ExitCode = 1, StandardOutput = string.Empty, StandardError = stderr, Duration = TimeSpan.Zero };
}

internal sealed class FakeAgentSession(FakeAgentRuntime runtime, AgentSessionSpec spec) : IAgentSession
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
