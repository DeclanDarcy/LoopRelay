using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Streaming;
using Microsoft.Extensions.Caching.Memory;

namespace CommandCenter.Orchestration.Services;

/// <summary>
/// Per-repository orchestrator. It owns ONLY transient run state and live Codex process handles
/// (planning + decision sessions, cached plan/handoff/decisions, iteration counter, router inputs,
/// and the three SSE channels). Every durable fact is reconstructed from repository artifacts, so a
/// process restart loses no authority — the live handles are convenience, not state of record.
///
/// It is compositional: it may call Agents, Execution, DecisionSessions, artifact, and Git services,
/// but it is not the semantic authority for any of those domains.
/// </summary>
public sealed class RepositoryOrchestrator : IAsyncDisposable
{
    private readonly IAgentRuntime agentRuntime;
    private readonly IArtifactStore artifactStore;
    private readonly IMemoryCache memoryCache;

    // Serializes session open/close and the dispose handoff so an in-flight EnsureSession can never
    // leak a handle past disposal. NOT disposed (avoids a Release-vs-Dispose ObjectDisposedException).
    private readonly SemaphoreSlim gate = new(1, 1);

    private IAgentSession? planningSession;
    private IAgentSession? decisionSession;
    private volatile bool disposed;

    public RepositoryOrchestrator(
        string repositoryId,
        IAgentRuntime agentRuntime,
        IArtifactStore artifactStore,
        IMemoryCache memoryCache)
    {
        RepositoryId = repositoryId;
        this.agentRuntime = agentRuntime;
        this.artifactStore = artifactStore;
        this.memoryCache = memoryCache;
    }

    public string RepositoryId { get; }

    public OrchestratorStreamChannel PlanningStream { get; } = new();

    public OrchestratorStreamChannel ExecutionStream { get; } = new();

    public OrchestratorStreamChannel DecisionStream { get; } = new();

    // ---- Transient run state (lost on restart; never the system of record) ----
    public string? CachedPlan { get; private set; }

    public string? CurrentHandoff { get; private set; }

    public string? CurrentDecisions { get; private set; }

    public int IterationCounter { get; private set; }

    public RouterInputs RouterInputs { get; private set; } = RouterInputs.Empty;

    public bool HasPlanningSession => planningSession is not null;

    public bool HasDecisionSession => decisionSession is not null;

    public bool IsDisposed => disposed;

    /// <summary>
    /// Reports plan existence + lifecycle state from the durable artifact, NOT from any live handle —
    /// so a freshly reconstructed orchestrator (no open sessions) answers correctly after a restart.
    /// </summary>
    public async Task<PlanStatus> GetPlanStatusAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);
        bool planExists = await artifactStore.ExistsAsync(planPath);
        return new PlanStatus(
            planExists,
            planExists ? PlanLifecycleState.ExecutingPlan : PlanLifecycleState.PlanAuthoring);
    }

    /// <summary>Opens (once) the held-open Operational planning process, reusing it on later calls.</summary>
    public Task<IAgentSession> EnsurePlanningSessionAsync(Repository repository, CancellationToken cancellationToken = default) =>
        EnsureSessionAsync(SessionSlot.Planning, BuildPlanningSpec(repository), cancellationToken);

    /// <summary>Opens (once) the held-open zero-permission Decision process, reusing it on later calls.</summary>
    public Task<IAgentSession> EnsureDecisionSessionAsync(Repository repository, CancellationToken cancellationToken = default) =>
        EnsureSessionAsync(SessionSlot.Decision, BuildDecisionSpec(repository), cancellationToken);

    public void RecordPlan(string plan)
    {
        CachedPlan = plan;
        TouchRunCache();
    }

    public void RecordHandoff(string handoff)
    {
        CurrentHandoff = handoff;
        TouchRunCache();
    }

    public void RecordDecisions(string decisions)
    {
        CurrentDecisions = decisions;
        TouchRunCache();
    }

    public void RecordRouterInputs(RouterInputs routerInputs)
    {
        RouterInputs = routerInputs;
        TouchRunCache();
    }

    public int AdvanceIteration()
    {
        IterationCounter++;
        TouchRunCache();
        return IterationCounter;
    }

    public async ValueTask DisposeAsync()
    {
        IAgentSession? planning;
        IAgentSession? decision;
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            planning = planningSession;
            decision = decisionSession;
            planningSession = null;
            decisionSession = null;
        }
        finally
        {
            gate.Release();
        }

        PlanningStream.Complete();
        ExecutionStream.Complete();
        DecisionStream.Complete();
        memoryCache.Remove(OrchestrationCacheKeys.PlanRun(RepositoryId));

        if (planning is not null)
        {
            await planning.DisposeAsync().ConfigureAwait(false);
        }

        if (decision is not null)
        {
            await decision.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<IAgentSession> EnsureSessionAsync(SessionSlot slot, AgentSessionSpec spec, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            IAgentSession? existing = slot == SessionSlot.Planning ? planningSession : decisionSession;
            if (existing is not null)
            {
                return existing;
            }

            IAgentSession opened = await agentRuntime.OpenSessionAsync(spec, cancellationToken).ConfigureAwait(false);
            if (slot == SessionSlot.Planning)
            {
                planningSession = opened;
            }
            else
            {
                decisionSession = opened;
            }

            return opened;
        }
        finally
        {
            gate.Release();
        }
    }

    private void TouchRunCache() =>
        memoryCache.Set(
            OrchestrationCacheKeys.PlanRun(RepositoryId),
            new ActiveRunSnapshot(IterationCounter, CachedPlan is not null));

    // Operational profile: can write the repository + .agents, approvals off, ExtraHigh effort.
    private AgentSessionSpec BuildPlanningSpec(Repository repository) =>
        new(
            SessionIdentity.New(),
            RepositoryId,
            SessionRole.Planning,
            new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
            repository.Path);

    // Decision profile: zero operational permissions, read-only, approvals never, ExtraHigh effort.
    private AgentSessionSpec BuildDecisionSpec(Repository repository) =>
        new(
            SessionIdentity.New(),
            RepositoryId,
            SessionRole.Decision,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, Identifier: "xhigh"),
            repository.Path);

    private enum SessionSlot
    {
        Planning,
        Decision
    }
}
