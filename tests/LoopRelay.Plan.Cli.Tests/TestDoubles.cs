using System.Collections.Concurrent;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Projections;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Plan.Cli;

namespace LoopRelay.Plan.Cli.Tests;

internal sealed class RecordingLoopConsole : Cli.ILoopConsole
{
    public ConcurrentQueue<(string Kind, string Text)> Events { get; } = new();
    public void Phase(string phase) => Events.Enqueue(("phase", phase));
    public void Message(string content) => Events.Enqueue(("message", content));
    public void Delta(string text) => Events.Enqueue(("delta", text));
    public void Tool(string summary) => Events.Enqueue(("tool", summary));
    public void Info(string text) => Events.Enqueue(("info", text));
    public void Warn(string text) => Events.Enqueue(("warn", text));
    public void Error(string text) => Events.Enqueue(("error", text));

    public IReadOnlyList<string> Messages =>
        Events.Where(e => e.Kind == "message").Select(e => e.Text).ToList();
}

/// <summary>A scripted codex turn: inspect (spec, prompt), optionally mutate the store, return a result.</summary>
internal sealed record ScriptedTurn(Func<AgentSessionSpec, string, IArtifactStore, AgentTurnResult> Handler);

/// <summary>
/// In-memory sandbox workspace factory. Records created/disposed roots. A test not about isolation can
/// set Root = repository.Path so the sandbox becomes transparent (the in-place rewrite effect resolves to the
/// repo path) and existing repo-writing scripts keep working unchanged.
/// </summary>
internal sealed class FakeSandboxWorkspaceFactory : ISandboxWorkspaceFactory
{
    public string Root { get; init; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cc-plan-cli-fake-sandbox", Guid.NewGuid().ToString("N"));

    public List<string> Created { get; } = new();

    public List<string> Disposed { get; } = new();

    public int CreatedCount => Created.Count;

    public string Resolve(string relativePath) =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, relativePath));

    public Task<ISandboxWorkspace> CreateAsync(string label, CancellationToken cancellationToken = default)
    {
        Created.Add(Root);
        return Task.FromResult<ISandboxWorkspace>(new FakeSandboxWorkspace(this));
    }

    private sealed class FakeSandboxWorkspace(FakeSandboxWorkspaceFactory owner) : ISandboxWorkspace
    {
        public string RootPath => owner.Root;

        public ValueTask DisposeAsync()
        {
            owner.Disposed.Add(owner.Root);
            return ValueTask.CompletedTask;
        }
    }
}

internal static class Turns
{
    public static AgentTurnResult Completed(string output) =>
        new(0, AgentTurnState.Completed, output, new AgentTokenUsage(0, 0));

    public static AgentTurnResult Failed(string output = "boom", string? diagnostics = null) =>
        new(0, AgentTurnState.Failed, output, new AgentTokenUsage(0, 0), diagnostics);
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
/// Scripts <see cref="IProcessRunner.RunAsync"/> (no real git, no real new-epic). Records every call in
/// invocation order, and returns whatever <see cref="Handler"/> produces (or a zero-exit success when Handler
/// is null). <see cref="OnRunAsync"/> is an async hook invoked (before Handler) for every call, so a test can
/// match a command (e.g. the new-epic invocation — the only non-git call) and simulate its filesystem side
/// effects against the in-memory artifact store. StartInteractiveAsync throws — the publisher and
/// rollover step only ever run RunAsync.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Args, string WorkingDirectory)> Calls { get; } = new();

    // Handler receives the working directory too, so a test can script the .agents submodule
    // (workingDirectory ends in ".agents") differently from the parent repo.
    public Func<string, IReadOnlyList<string>, ProcessRunResult>? Handler { get; set; }

    /// <summary>Async side-effect hook: (fileName, args, workingDirectory) → Task, run before Handler.</summary>
    public Func<string, IReadOnlyList<string>, string, Task>? OnRunAsync { get; set; }

    public async Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        Calls.Add((fileName, arguments, workingDirectory));
        if (OnRunAsync is not null)
        {
            await OnRunAsync(fileName, arguments, workingDirectory);
        }

        return Handler?.Invoke(workingDirectory, arguments) ?? Ok();
    }

    public Task<IAgentProcess> StartInteractiveAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public static ProcessRunResult Ok(string stdout = "") =>
        new() { ExitCode = 0, StandardOutput = stdout, StandardError = string.Empty, Duration = TimeSpan.Zero };

    public static ProcessRunResult Fail(string stderr, int exitCode = 1, string stdout = "") =>
        new() { ExitCode = exitCode, StandardOutput = stdout, StandardError = stderr, Duration = TimeSpan.Zero };
}

internal sealed class FakeDecisionSessionResumeStore : IDecisionSessionResumeStore
{
    public DecisionSessionResumeState? State { get; set; }
    public List<DecisionSessionResumeState> Written { get; } = new();
    public int ClearCalls { get; private set; }

    public Task<DecisionSessionResumeState?> ReadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(State);

    public Task WriteAsync(DecisionSessionResumeState state, CancellationToken cancellationToken = default)
    {
        Written.Add(state);
        State = state;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        ClearCalls++;
        State = null;
        return Task.CompletedTask;
    }
}

internal sealed class FakeProjectionService(string content = "PROJECT CONTEXT PROJECTION") : IProjectContextProjectionService
{
    public int EnsureFreshCalls { get; private set; }
    public int EvaluateFreshnessCalls { get; private set; }
    public List<string> RuntimePromptNames { get; } = new();

    public Task<ProjectContextProjectionResult> EnsureFreshAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default)
    {
        EnsureFreshCalls++;
        RuntimePromptNames.Add(runtimePromptName);
        return Task.FromResult(new ProjectContextProjectionResult(
            new ProjectionDefinition(
                runtimePromptName,
                $"ProjectionFor{runtimePromptName}",
                ProjectionArtifactPaths.ProjectionPaths[runtimePromptName],
                "# Test Projection",
                runtimePromptName),
            content,
            Generated: true,
            ProjectionStaleStatus.Fresh,
            []));
    }

    public Task<ProjectionFreshness> EvaluateFreshnessAsync(
        string runtimePromptName,
        CancellationToken cancellationToken = default)
    {
        EvaluateFreshnessCalls++;
        RuntimePromptNames.Add(runtimePromptName);
        return Task.FromResult(ProjectionFreshness.Fresh);
    }
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
    public string? ThreadId => null;

    public Task<AgentTurnResult> RunTurnAsync(
        string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken ct = default) =>
        Task.FromResult(runtime.RunSessionTurn(spec, prompt));

    public Task CancelAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
