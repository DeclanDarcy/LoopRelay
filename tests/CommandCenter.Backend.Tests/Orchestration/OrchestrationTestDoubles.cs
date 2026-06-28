using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Services;
using Microsoft.Extensions.Caching.Memory;

namespace CommandCenter.Backend.Tests.Orchestration;

/// <summary>Records every session opened and hands back a disposable fake per open.</summary>
internal sealed class FakeAgentRuntime : IAgentRuntime
{
    public List<AgentSessionSpec> OpenedSpecs { get; } = new();

    public List<FakeAgentSession> Sessions { get; } = new();

    public int OpenCount => OpenedSpecs.Count;

    /// <summary>When set, every opened session blocks its DisposeAsync until this task completes —
    /// lets a test hold a teardown mid-flight to observe the registry's create/teardown serialization.</summary>
    public Task? SessionDisposeGate { get; set; }

    /// <summary>Side effect run during each turn (e.g. simulate Codex writing <c>.agents/plan.md</c>).</summary>
    public Func<Task>? OnTurn { get; set; }

    /// <summary>Stdout chunks streamed (in order) before each turn completes.</summary>
    public IReadOnlyList<string> ScriptedChunks { get; set; } = Array.Empty<string>();

    /// <summary>When set, every turn parks (after recording the prompt) until this task completes — lets a
    /// test hold a planning turn ACTIVE to observe the concurrent-turn (409) rejection and the dispose drain.</summary>
    public Task? TurnGate { get; set; }

    /// <summary>Terminal state every turn reports.</summary>
    public AgentTurnState TurnState { get; set; } = AgentTurnState.Completed;

    /// <summary>The <see cref="AgentTurnResult.Output"/> every turn reports (surfaces as the failed-event detail).</summary>
    public string TurnOutput { get; set; } = string.Empty;

    /// <summary>Usage every turn reports.</summary>
    public AgentTokenUsage TurnUsage { get; set; } = new(10, 20);

    public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken cancellationToken = default)
    {
        OpenedSpecs.Add(spec);
        var session = new FakeAgentSession(spec, this);
        Sessions.Add(session);
        return Task.FromResult<IAgentSession>(session);
    }

    public Task<AgentTurnResult> RunOneShotAsync(
        AgentSessionSpec spec,
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AgentTurnResult(0, AgentTurnState.Completed, string.Empty, AgentTokenUsage.Zero));
}

internal sealed class FakeAgentSession : IAgentSession
{
    private readonly AgentSessionSpec spec;
    private readonly FakeAgentRuntime runtime;
    private int turnCount;

    public FakeAgentSession(AgentSessionSpec spec, FakeAgentRuntime runtime)
    {
        this.spec = spec;
        this.runtime = runtime;
        SessionId = spec.SessionId;
    }

    public bool Disposed { get; private set; }

    /// <summary>Every prompt this session was asked to run, in order.</summary>
    public List<string> Prompts { get; } = new();

    public SessionIdentity SessionId { get; }

    public string RepositoryId => spec.RepositoryId;

    public SessionRole Role => spec.Role;

    public AgentSessionMode Mode => AgentSessionMode.Persistent;

    public AgentProcessState State => Disposed ? AgentProcessState.Disposed : AgentProcessState.Running;

    public int CompletedTurns => turnCount;

    public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;

    public async Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default)
    {
        Prompts.Add(prompt);

        if (runtime.TurnGate is not null)
        {
            await runtime.TurnGate.ConfigureAwait(false);
        }

        if (onChunk is not null)
        {
            foreach (string chunk in runtime.ScriptedChunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await onChunk(new AgentStreamChunk(turnCount, AgentProcessOutputStream.StandardOutput, chunk));
            }
        }

        if (runtime.OnTurn is not null)
        {
            await runtime.OnTurn();
        }

        cancellationToken.ThrowIfCancellationRequested();
        int index = turnCount++;
        return new AgentTurnResult(index, runtime.TurnState, runtime.TurnOutput, runtime.TurnUsage);
    }

    public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (runtime.SessionDisposeGate is not null)
        {
            await runtime.SessionDisposeGate.ConfigureAwait(false);
        }

        Disposed = true;
    }
}

/// <summary>In-memory artifact store. <see cref="ExistsResult"/> forces an existence answer for the
/// plan-status gate; writes are observable through <see cref="ReadAsync"/> and <see cref="ExistsAsync"/>.</summary>
internal sealed class FakeArtifactStore : IArtifactStore
{
    private readonly Dictionary<string, string> files = new(StringComparer.Ordinal);

    /// <summary>When true, every path reports as existing regardless of writes (drives plan-status tests).</summary>
    public bool ExistsResult { get; set; }

    public List<string> ExistsQueries { get; } = new();

    public List<string> WriteQueries { get; } = new();

    public Task<bool> ExistsAsync(string path)
    {
        ExistsQueries.Add(path);
        return Task.FromResult(ExistsResult || files.ContainsKey(path));
    }

    public Task<string?> ReadAsync(string path) =>
        Task.FromResult(files.TryGetValue(path, out string? content) ? content : null);

    public Task WriteAsync(string path, string content)
    {
        WriteQueries.Add(path);
        files[path] = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path)
    {
        files.Remove(path);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) =>
        Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}

internal static class OrchestrationTestFactory
{
    public static Repository Repository(string? path = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "fixture",
            Path = path ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")),
        };

    public static MemoryCache Cache() => new(new MemoryCacheOptions());

    public static RepositoryOrchestratorRegistry Registry(
        FakeAgentRuntime? runtime = null,
        FakeArtifactStore? store = null,
        MemoryCache? cache = null) =>
        new(runtime ?? new FakeAgentRuntime(), store ?? new FakeArtifactStore(), cache ?? Cache());

    public static RepositoryOrchestrator Orchestrator(
        string? repositoryId = null,
        FakeAgentRuntime? runtime = null,
        FakeArtifactStore? store = null,
        MemoryCache? cache = null) =>
        new(
            repositoryId ?? Guid.NewGuid().ToString("D"),
            runtime ?? new FakeAgentRuntime(),
            store ?? new FakeArtifactStore(),
            cache ?? Cache());
}
