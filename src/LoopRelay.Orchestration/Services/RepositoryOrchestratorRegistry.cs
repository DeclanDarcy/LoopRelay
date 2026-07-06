using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace LoopRelay.Orchestration.Services;

/// <summary>
/// Process-wide singleton that hands out EXACTLY ONE live <see cref="RepositoryOrchestrator"/> per
/// repository id. Two mechanisms combine to make a duplicate impossible even under concurrency:
/// <list type="bullet">
/// <item>the dictionary value is a <see cref="Lazy{T}"/> with
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>, so even when
/// <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/> races and
/// builds several lazies, only the stored one is ever forced; and</item>
/// <item>creation (<see cref="GetOrCreateAsync"/>) and teardown (<see cref="RemoveAsync"/>) are
/// serialized by <c>mutationGate</c>, and teardown holds the gate across the orchestrator's full
/// <c>DisposeAsync</c> — so a replacement is published only AFTER the prior instance is completely
/// disposed. Without this, <see cref="RemoveAsync"/> would drop the entry, then yield while disposing
/// live Codex handles, and a concurrent create would publish a second live orchestrator for the same
/// id during that window.</item>
/// </list>
/// Orchestrators hold live process handles, never durable state, so the registry itself persists
/// nothing across a restart. (The gate is registry-wide; per-id locking is a later optimization if
/// cross-repository teardown contention ever matters — teardown is rare and short.)
/// </summary>
public sealed class RepositoryOrchestratorRegistry : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<RepositoryOrchestrator>> orchestrators =
        new(StringComparer.Ordinal);

    // Serializes structural mutations (create-new-entry, remove+dispose) so a fresh orchestrator is
    // never published while the prior one for the same id is still tearing down. NOT disposed
    // (singleton-lifetime; avoids a Release-vs-Dispose ObjectDisposedException).
    private readonly SemaphoreSlim mutationGate = new(1, 1);

    private readonly IAgentRuntime agentRuntime;
    private readonly IArtifactStore artifactStore;
    private readonly IMemoryCache memoryCache;
    private readonly IPlanArtifactPublisher planArtifactPublisher;
    private readonly IDecisionSessionRouter decisionSessionRouter;
    private readonly IDecisionCostModel costModel;
    private readonly ISandboxWorkspaceFactory sandboxWorkspaceFactory;
    private readonly OrchestrationFeatureFlags flags;

    public RepositoryOrchestratorRegistry(
        IAgentRuntime agentRuntime,
        IArtifactStore artifactStore,
        IMemoryCache memoryCache,
        IPlanArtifactPublisher planArtifactPublisher,
        IDecisionSessionRouter decisionSessionRouter,
        OrchestrationFeatureFlags? flags = null,
        IDecisionCostModel? costModel = null,
        ISandboxWorkspaceFactory? sandboxWorkspaceFactory = null)
    {
        this.agentRuntime = agentRuntime;
        this.artifactStore = artifactStore;
        this.memoryCache = memoryCache;
        this.planArtifactPublisher = planArtifactPublisher;
        this.decisionSessionRouter = decisionSessionRouter;
        // A null/default-constructed flags object reproduces today's behavior byte-for-byte (m10, additive only).
        this.flags = flags ?? new OrchestrationFeatureFlags();
        // Threads the DI-registered cost model into every orchestrator so a deployment can swap the seam; the
        // null default matches the orchestrator's own default (EffectiveTokenCostModel).
        this.costModel = costModel ?? new EffectiveTokenCostModel();
        // Same seam-threading for the Stage-2 sandbox workspace factory; null default = real temp directories.
        this.sandboxWorkspaceFactory = sandboxWorkspaceFactory ?? new TempSandboxWorkspaceFactory();
    }

    public int Count => orchestrators.Count;

    public async Task<RepositoryOrchestrator> GetOrCreateAsync(string repositoryId, CancellationToken cancellationToken = default)
    {
        await mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return orchestrators.GetOrAdd(
                repositoryId,
                id => new Lazy<RepositoryOrchestrator>(
                    () => new RepositoryOrchestrator(id, agentRuntime, artifactStore, memoryCache, planArtifactPublisher, decisionSessionRouter, flags, costModel, sandboxWorkspaceFactory),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }
        finally
        {
            mutationGate.Release();
        }
    }

    public bool TryGet(string repositoryId, [MaybeNullWhen(false)] out RepositoryOrchestrator orchestrator)
    {
        if (orchestrators.TryGetValue(repositoryId, out Lazy<RepositoryOrchestrator>? lazy) && lazy.IsValueCreated)
        {
            orchestrator = lazy.Value;
            return true;
        }

        orchestrator = null;
        return false;
    }

    /// <summary>
    /// Tears down a repository's orchestrator (deselection, failure, runtime teardown). Holds
    /// <c>mutationGate</c> across the entire remove + dispose so no replacement can be published
    /// for this id until disposal completes.
    /// </summary>
    public async Task<bool> RemoveAsync(string repositoryId)
    {
        await mutationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (orchestrators.TryRemove(repositoryId, out Lazy<RepositoryOrchestrator>? lazy))
            {
                if (lazy.IsValueCreated)
                {
                    await lazy.Value.DisposeAsync().ConfigureAwait(false);
                }

                return true;
            }

            return false;
        }
        finally
        {
            mutationGate.Release();
        }
    }

    /// <summary>Disposes every orchestrator on application shutdown (singleton disposal).</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (string repositoryId in orchestrators.Keys.ToArray())
        {
            await RemoveAsync(repositoryId).ConfigureAwait(false);
        }
    }
}
