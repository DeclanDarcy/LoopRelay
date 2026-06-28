using System.Text.Json;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
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
    private static readonly JsonSerializerOptions StreamJson = new(JsonSerializerDefaults.Web);

    private readonly IAgentRuntime agentRuntime;
    private readonly IArtifactStore artifactStore;
    private readonly IMemoryCache memoryCache;

    // Serializes session open/close and the dispose handoff so an in-flight EnsureSession can never
    // leak a handle past disposal. NOT disposed (avoids a Release-vs-Dispose ObjectDisposedException).
    private readonly SemaphoreSlim gate = new(1, 1);

    // Cancelled on disposal. The background planning turn runs on THIS token, never on an HTTP
    // request token — so completing the write/revise POST does not abort the streaming turn.
    private readonly CancellationTokenSource lifetimeCts = new();

    private readonly List<PromptProvenance> planningProvenance = new();

    private IAgentSession? planningSession;
    private IAgentSession? decisionSession;

    // The single in-flight planning turn (write or revise). Read by DisposeAsync to drain before
    // completing the stream; written by LaunchPlanningTurn. Volatile for cross-thread visibility.
    private volatile Task? planningTurn;

    // 0 = idle, 1 = a planning turn is running. Interlocked claim/release serializes write vs revise
    // so a second authoring command is rejected (409) rather than racing the warm process.
    private int planningTurnActive;

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

    /// <summary>True while a Write/Revise planning turn is streaming.</summary>
    public bool IsPlanningTurnActive => Volatile.Read(ref planningTurnActive) != 0;

    /// <summary>The in-flight planning turn, or a completed task when idle. Lets callers/tests await turn completion.</summary>
    public Task PlanningTurnTask => planningTurn ?? Task.CompletedTask;

    /// <summary>Provenance recorded at issuance for the initial plan and every revision (m3).</summary>
    public IReadOnlyList<PromptProvenance> PlanningProvenance => planningProvenance;

    public PromptProvenance? LastPlanningProvenance =>
        planningProvenance.Count > 0 ? planningProvenance[^1] : null;

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

    /// <summary>
    /// Write Plan (m3): persist the Roadmap and Specs BEFORE the prompt runs, open/reuse the held-open
    /// Operational planning process, select <c>WritePlanForNewCodebase</c>/<c>WritePlanAgainstCodebase</c>,
    /// and stream the turn to <see cref="PlanningStream"/>. The turn runs in the background on the
    /// orchestrator's lifetime; this method returns once the turn is launched.
    /// </summary>
    public async Task BeginWritePlanAsync(Repository repository, PlanWriteRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(request.Roadmap))
        {
            throw new ArgumentException("Roadmap is required to write a plan.", nameof(request));
        }

        ClaimPlanningTurn();
        try
        {
            await PersistPlanInputsAsync(repository, request).ConfigureAwait(false);
            IAgentSession session = await EnsurePlanningSessionAsync(repository, cancellationToken).ConfigureAwait(false);
            (string promptText, PromptProvenance provenance) = BuildWritePlan(request);
            await LaunchPlanningTurnAsync(session, repository, promptText, provenance, "WritePlan").ConfigureAwait(false);
        }
        catch
        {
            ReleasePlanningTurn();
            throw;
        }
    }

    /// <summary>
    /// Revise Plan (m3): submit <c>RevisePlan.Render(feedback)</c> to the SAME held-open planning
    /// process (warm-process reuse). Fails if no planning process is open yet.
    /// </summary>
    public async Task BeginRevisePlanAsync(Repository repository, PlanReviseRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(request.Feedback))
        {
            throw new ArgumentException("Feedback is required to revise a plan.", nameof(request));
        }

        ClaimPlanningTurn();
        try
        {
            (string promptText, PromptProvenance provenance) = BuildRevisePlan(request.Feedback);
            // Pass no session: the warm planning process is captured under the gate INSIDE the launch,
            // so a concurrent ExecutePlan/Dispose cannot tear it down between a null-check and the turn.
            await LaunchPlanningTurnAsync(session: null, repository, promptText, provenance, "RevisePlan").ConfigureAwait(false);
        }
        catch
        {
            ReleasePlanningTurn();
            throw;
        }
    }

    /// <summary>
    /// Execute Plan (m3 owns: verify the plan exists and close the held-open planning process). The
    /// Phase 4 work — milestone extraction, commit, push, and starting operational execution — is
    /// deliberately NOT done here yet; this is the handoff seam to m4.
    /// </summary>
    public async Task ExecutePlanAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsPlanningTurnActive)
        {
            throw new InvalidOperationException("A planning turn is still running; wait for it to complete before executing.");
        }

        string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);
        if (!await artifactStore.ExistsAsync(planPath).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Cannot execute: .agents/plan.md does not exist yet.");
        }

        await ClosePlanningSessionAsync().ConfigureAwait(false);
    }

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
        Task? turn;
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
            // Capture under the gate: LaunchPlanningTurnAsync assigns planningTurn under the SAME gate with
            // a disposed re-check, so an in-flight turn is always seen here, and a turn that would start
            // after this point observes disposed==true and never launches (nothing to drain is missed).
            turn = planningTurn;
        }
        finally
        {
            gate.Release();
        }

        // Cancel the lifetime FIRST so the in-flight planning turn observes cancellation and stops
        // publishing, THEN drain it, and only THEN complete the streams — otherwise a late Publish
        // would hit a completed channel (which fail-fast throws).
        lifetimeCts.Cancel();
        if (turn is not null)
        {
            try
            {
                await turn.ConfigureAwait(false);
            }
            catch
            {
                // Best-effort drain during teardown; the turn's own failure path already reported it.
            }
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

        lifetimeCts.Dispose();
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

    private async Task ClosePlanningSessionAsync()
    {
        IAgentSession? planning;
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            planning = planningSession;
            planningSession = null;
        }
        finally
        {
            gate.Release();
        }

        if (planning is not null)
        {
            await planning.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task PersistPlanInputsAsync(Repository repository, PlanWriteRequest request)
    {
        await artifactStore.WriteAsync(
            ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.SpecsRoadmap),
            request.Roadmap).ConfigureAwait(false);

        for (int index = 0; index < request.Specs.Count; index++)
        {
            await artifactStore.WriteAsync(
                ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Spec(index + 1)),
                request.Specs[index]).ConfigureAwait(false);
        }
    }

    // Captures the session + lifetime token and publishes the background turn ATOMICALLY with disposal:
    // the assignment runs under `gate` with a fresh `disposed` re-check, so a turn is never launched after
    // DisposeAsync flips `disposed`, and a turn launched before it is always visible to the dispose drain.
    // Passing session==null resolves the warm planning session under the same gate (revise reuse), so a
    // concurrent ClosePlanningSession/Dispose cannot tear it down between the null-check and the turn.
    private async Task LaunchPlanningTurnAsync(
        IAgentSession? session,
        Repository repository,
        string promptText,
        PromptProvenance provenance,
        string phase)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            IAgentSession resolved = session
                ?? planningSession
                ?? throw new InvalidOperationException("No warm planning session to revise. Write a plan first.");

            planningProvenance.Add(provenance);
            CancellationToken token = lifetimeCts.Token;
            planningTurn = Task.Run(() => RunPlanningTurnAsync(resolved, repository, promptText, phase, token));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RunPlanningTurnAsync(
        IAgentSession session,
        Repository repository,
        string promptText,
        string phase,
        CancellationToken cancellationToken)
    {
        try
        {
            PlanningStream.Publish("turn-started", Serialize(new { phase }));

            AgentTurnResult result = await session.RunTurnAsync(
                promptText,
                chunk =>
                {
                    if (chunk.Stream == AgentProcessOutputStream.StandardOutput && !string.IsNullOrEmpty(chunk.Content))
                    {
                        PlanningStream.Publish("delta", Serialize(new { text = chunk.Content }));
                    }

                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(false);

            if (result.State != AgentTurnState.Completed)
            {
                PlanningStream.Publish("failed", Serialize(new { reason = DescribeTerminalState(result.State), detail = result.Output }));
                return;
            }

            string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);
            if (!await artifactStore.ExistsAsync(planPath).ConfigureAwait(false))
            {
                PlanningStream.Publish("failed", Serialize(new { reason = "Planning turn completed but .agents/plan.md was not written." }));
                return;
            }

            string plan = await artifactStore.ReadAsync(planPath).ConfigureAwait(false) ?? string.Empty;
            RecordPlan(plan);
            PlanningStream.Publish("completed", Serialize(new
            {
                plan,
                promptTokens = result.Usage.PromptTokens,
                outputTokens = result.Usage.OutputTokens,
            }));
        }
        catch (OperationCanceledException)
        {
            // Disposing (lifetime cancelled). The stream is about to Complete(); do not publish into it.
        }
        catch (Exception exception)
        {
            TryPublishFailed(exception.Message);
        }
        finally
        {
            ReleasePlanningTurn();
        }
    }

    private void TryPublishFailed(string reason)
    {
        try
        {
            PlanningStream.Publish("failed", Serialize(new { reason }));
        }
        catch (InvalidOperationException)
        {
            // Stream already completed during teardown; nothing left to notify.
        }
    }

    private void ClaimPlanningTurn()
    {
        if (Interlocked.CompareExchange(ref planningTurnActive, 1, 0) != 0)
        {
            throw new InvalidOperationException("A planning turn is already running for this repository.");
        }
    }

    private void ReleasePlanningTurn() => Interlocked.Exchange(ref planningTurnActive, 0);

    private static (string Prompt, PromptProvenance Provenance) BuildWritePlan(PlanWriteRequest request)
    {
        var inputs = new List<string> { OrchestrationArtifactPaths.SpecsRoadmap };
        for (int index = 0; index < request.Specs.Count; index++)
        {
            inputs.Add(OrchestrationArtifactPaths.Spec(index + 1));
        }

        string[] outputs = { OrchestrationArtifactPaths.Plan };

        return request.NewCodebase
            ? (WritePlanForNewCodebase.Text, new PromptProvenance
            {
                PromptName = nameof(WritePlanForNewCodebase),
                PromptType = typeof(WritePlanForNewCodebase).FullName!,
                SourceHash = WritePlanForNewCodebase.SourceHash,
                SessionRole = PromptSessionRole.Planning,
                WorkflowPhase = "WritePlan",
                InputArtifactIdentities = inputs,
                OutputArtifactIdentities = outputs,
            })
            : (WritePlanAgainstCodebase.Text, new PromptProvenance
            {
                PromptName = nameof(WritePlanAgainstCodebase),
                PromptType = typeof(WritePlanAgainstCodebase).FullName!,
                SourceHash = WritePlanAgainstCodebase.SourceHash,
                SessionRole = PromptSessionRole.Planning,
                WorkflowPhase = "WritePlan",
                InputArtifactIdentities = inputs,
                OutputArtifactIdentities = outputs,
            });
    }

    private static (string Prompt, PromptProvenance Provenance) BuildRevisePlan(string feedback) =>
        (RevisePlan.Render(feedback), new PromptProvenance
        {
            PromptName = nameof(RevisePlan),
            PromptType = typeof(RevisePlan).FullName!,
            SourceHash = RevisePlan.SourceHash,
            SessionRole = PromptSessionRole.Planning,
            WorkflowPhase = "RevisePlan",
            InputArtifactIdentities = new[] { OrchestrationArtifactPaths.Plan },
            OutputArtifactIdentities = new[] { OrchestrationArtifactPaths.Plan },
        });

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, StreamJson);

    // Human-readable failure reason for the SSE `failed` event — never the raw enum token, so the UI
    // can render it verbatim (matching the descriptive reason the plan-not-written branch emits).
    private static string DescribeTerminalState(AgentTurnState state) => state switch
    {
        AgentTurnState.Failed => "The planning agent run failed.",
        AgentTurnState.Canceled => "The planning run was cancelled.",
        _ => $"The planning run ended in an unexpected state ({state}).",
    };

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
