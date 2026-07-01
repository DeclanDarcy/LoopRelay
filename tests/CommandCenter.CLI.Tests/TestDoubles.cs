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

/// <summary>
/// In-memory sandbox workspace factory (Stage 2). Records created/disposed roots. A test not about isolation can
/// set Root = repository.Path so the sandbox becomes transparent (the in-place rewrite effect resolves to the
/// repo path) and existing repo-writing transfer scripts keep working unchanged.
/// </summary>
internal sealed class FakeSandboxWorkspaceFactory : ISandboxWorkspaceFactory
{
    public string Root { get; init; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cc-cli-fake-sandbox", Guid.NewGuid().ToString("N"));

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
    public List<(string FileName, IReadOnlyList<string> Args, string WorkingDirectory)> Calls { get; } = new();

    // Handler receives the working directory too, so a test can script the .agents submodule
    // (workingDirectory ends in ".agents") differently from the parent repo.
    public Func<string, IReadOnlyList<string>, ProcessRunResult>? Handler { get; set; }

    public Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
    {
        Calls.Add((fileName, arguments, workingDirectory));
        return Task.FromResult(Handler?.Invoke(workingDirectory, arguments) ?? Ok());
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

/// <summary>Returns scripted <see cref="CodexUsageStatus"/> snapshots for the usage gate (no real codex).</summary>
internal sealed class FakeCodexUsageProbe : ICodexUsageProbe
{
    public Queue<CodexUsageStatus?> Results { get; } = new();

    /// <summary>Returned when <see cref="Results"/> is empty (the steady-state answer).</summary>
    public CodexUsageStatus? Default { get; set; }

    public int Calls { get; private set; }

    public Task<CodexUsageStatus?> QueryAsync(CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : Default);
    }
}

/// <summary>Records requested delay durations instead of actually sleeping.</summary>
internal sealed class FakeUsageDelay : IUsageDelay
{
    public List<TimeSpan> Delays { get; } = new();

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(duration);
        return Task.CompletedTask;
    }
}

internal sealed class FakeExecutableResolver : IAgentExecutableResolver
{
    public string Executable { get; init; } = "codex.exe";

    public string Resolve() => Executable;
}

/// <summary>Serves a single scripted interactive process (the codex /usage session) via StartInteractiveAsync.</summary>
internal sealed class FakeInteractiveProcessRunner(FakeAgentProcess process) : IProcessRunner
{
    public List<(string FileName, IReadOnlyList<string> Args, string WorkingDirectory)> InteractiveCalls { get; } = new();

    public Task<IAgentProcess> StartInteractiveAsync(
        string fileName, IReadOnlyList<string> arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        InteractiveCalls.Add((fileName, arguments, workingDirectory));
        return Task.FromResult<IAgentProcess>(process);
    }

    public Task<ProcessRunResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string workingDirectory) =>
        throw new NotSupportedException();

    public Task<ProcessStartResult> StartAsync(
        string fileName, IReadOnlyList<string> arguments, string workingDirectory,
        string? standardInput = null, Func<string, Task>? onStandardOutput = null,
        Func<string, Task>? onStandardError = null, Func<int?, Task>? onExit = null) =>
        throw new NotSupportedException();
}

/// <summary>
/// A scripted interactive codex process: streams <c>lines</c> from ReadOutputLinesAsync (optionally hanging
/// afterwards to exercise the scrape timeout), records prompts written, and tracks disposal.
/// </summary>
internal sealed class FakeAgentProcess(IEnumerable<string> lines, bool hangAfterLines = false) : IAgentProcess
{
    private readonly IReadOnlyList<string> lines = lines.ToList();

    public List<string> PromptsWritten { get; } = new();
    public int LinesEmitted { get; private set; }
    public bool Disposed { get; private set; }

    public int ProcessId => 1;
    public AgentProcessState State => AgentProcessState.Running;
    public int? ExitCode => null;
    public bool HasExited => false;
    public Task Completion => Task.CompletedTask;

    public Task WriteStandardInputAsync(string standardInput, CancellationToken cancellationToken = default) =>
        WritePromptAsync(standardInput, cancellationToken);

    public Task WritePromptAsync(string text, CancellationToken cancellationToken = default)
    {
        PromptsWritten.Add(text);
        return Task.CompletedTask;
    }

    public Task CompleteInputAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<string> ReadOutputLinesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (string line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LinesEmitted++;
            yield return line;
        }

        if (hangAfterLines)
        {
            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
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

    public Task<AgentTurnResult> RunTurnAsync(
        string prompt, Func<AgentStreamChunk, Task>? onChunk = null, CancellationToken ct = default) =>
        Task.FromResult(runtime.RunSessionTurn(spec, prompt));

    public Task CancelAsync(CancellationToken ct = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
