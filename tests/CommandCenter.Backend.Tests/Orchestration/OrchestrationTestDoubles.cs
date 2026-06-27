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

    public Task<IAgentSession> OpenSessionAsync(AgentSessionSpec spec, CancellationToken cancellationToken = default)
    {
        OpenedSpecs.Add(spec);
        var session = new FakeAgentSession(spec, SessionDisposeGate);
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
    private readonly Task? disposeGate;

    public FakeAgentSession(AgentSessionSpec spec, Task? disposeGate = null)
    {
        this.spec = spec;
        this.disposeGate = disposeGate;
        SessionId = spec.SessionId;
    }

    public bool Disposed { get; private set; }

    public SessionIdentity SessionId { get; }

    public string RepositoryId => spec.RepositoryId;

    public SessionRole Role => spec.Role;

    public AgentSessionMode Mode => AgentSessionMode.Persistent;

    public AgentProcessState State => Disposed ? AgentProcessState.Disposed : AgentProcessState.Running;

    public int CompletedTurns => 0;

    public AgentTokenUsage TotalUsage => AgentTokenUsage.Zero;

    public Task<AgentTurnResult> RunTurnAsync(
        string prompt,
        Func<AgentStreamChunk, Task>? onChunk = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AgentTurnResult(0, AgentTurnState.Completed, string.Empty, AgentTokenUsage.Zero));

    public Task CancelAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (disposeGate is not null)
        {
            await disposeGate.ConfigureAwait(false);
        }

        Disposed = true;
    }
}

/// <summary>Artifact store whose existence answer is configurable; records every queried path.</summary>
internal sealed class FakeArtifactStore : IArtifactStore
{
    public bool ExistsResult { get; set; }

    public List<string> ExistsQueries { get; } = new();

    public Task<bool> ExistsAsync(string path)
    {
        ExistsQueries.Add(path);
        return Task.FromResult(ExistsResult);
    }

    public Task<string?> ReadAsync(string path) => Task.FromResult<string?>(null);

    public Task WriteAsync(string path, string content) => Task.CompletedTask;

    public Task DeleteAsync(string path) => Task.CompletedTask;

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
