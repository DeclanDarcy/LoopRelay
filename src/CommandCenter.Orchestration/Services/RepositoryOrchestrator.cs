using System.Linq;
using System.Text.Json;
using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration.Abstractions;
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
    private readonly IPlanArtifactPublisher planArtifactPublisher;
    private readonly IDecisionSessionRouter decisionSessionRouter;

    // Serializes session open/close and the dispose handoff so an in-flight EnsureSession can never
    // leak a handle past disposal. NOT disposed (avoids a Release-vs-Dispose ObjectDisposedException).
    private readonly SemaphoreSlim gate = new(1, 1);

    // Cancelled on disposal. The background planning turn and execution run run on THIS token, never on
    // an HTTP request token — so completing the write/revise/execute POST does not abort the streaming run.
    private readonly CancellationTokenSource lifetimeCts = new();

    private readonly List<PromptProvenance> planningProvenance = new();
    private readonly List<PromptProvenance> executionProvenance = new();
    private readonly List<PromptProvenance> decisionProvenance = new();

    private IAgentSession? planningSession;
    private IAgentSession? decisionSession;

    // The single in-flight planning turn (write or revise). Read by DisposeAsync to drain before
    // completing the stream; written by LaunchPlanningTurn. Volatile for cross-thread visibility.
    private volatile Task? planningTurn;

    // The single in-flight Execute Plan run (m4). Same drain-before-complete contract as planningTurn.
    private volatile Task? executionRun;

    // The single in-flight Decision Runtime run (m5). Same drain-before-complete contract as the others.
    private volatile Task? decisionRun;

    // A SINGLE atomic gate over the repository: Idle | Planning | Executing. One CompareExchange both
    // claims the gate AND enforces mutual exclusion, so a planning turn and an execution run can never be
    // claimed concurrently. (Two separate flags were TOCTOU-racy: each side check-read the OTHER's flag
    // then claimed its OWN, across an await, so a concurrent execute+write could pass both checks and run
    // a planning turn and an execution run at once — tearing the planning session down mid-turn.)
    private const int RunStateIdle = 0;
    private const int RunStatePlanning = 1;
    private const int RunStateExecuting = 2;
    private int runState;

    // Run-scoped monotonic counter behind the 4-digit handoff rotation (handoff.0001.md, 0002.md, ...).
    private int handoffSequence;

    // A SEPARATE atomic gate for the Decision Runtime (m5), INDEPENDENT of runState. The decision process
    // is zero-permission and read-only — it has no operational authority and cannot tear anything down — so
    // a decision run is allowed to overlap a planning turn or an execution run (indeed the seed prompt is
    // literally "execution is starting"). This gate only serializes decision runs against EACH OTHER and is
    // what the dispose drain waits on. A single CompareExchange both claims the gate and rejects a second run.
    private const int DecisionStateIdle = 0;
    private const int DecisionStateActive = 1;
    private int decisionState;

    // True once the held-open decision process has received its StartDecisionSession seed turn. Touched only
    // by the single active decision run (serialized by decisionState), so a volatile read/write is enough.
    private volatile bool decisionSeeded;

    // Observed token accounting (m7), the router's routing signal: tokens seen on the LIVE decision process
    // (reset to 0 when it is recycled/closed, so it reflects pressure on the current process, not lifetime)
    // and cumulative operational continuation tokens. ComputeRouterInputs surfaces these through RouterInputs
    // and feeds them to the router; a deterministic content estimate is the fallback before any turn is
    // observed. Interlocked because the continuation run and the decision run accumulate on different threads.
    private int decisionSessionTokens;
    private int operationalSessionTokens;

    // The flow-specific conversation projection (m6): an append-only transcript of the loop's turns. Guarded
    // by its own lock because it is appended from background runs (planning/execution/decision) and read by
    // the GET /conversation endpoint concurrently.
    private readonly List<ConversationEntry> conversation = new();
    private int conversationSequence;

    private volatile bool disposed;

    public RepositoryOrchestrator(
        string repositoryId,
        IAgentRuntime agentRuntime,
        IArtifactStore artifactStore,
        IMemoryCache memoryCache,
        IPlanArtifactPublisher planArtifactPublisher,
        IDecisionSessionRouter decisionSessionRouter)
    {
        RepositoryId = repositoryId;
        this.agentRuntime = agentRuntime;
        this.artifactStore = artifactStore;
        this.memoryCache = memoryCache;
        this.planArtifactPublisher = planArtifactPublisher;
        this.decisionSessionRouter = decisionSessionRouter;
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
    public bool IsPlanningTurnActive => Volatile.Read(ref runState) == RunStatePlanning;

    /// <summary>True while an Execute Plan run (milestone extraction + start execution) is in progress (m4).</summary>
    public bool IsExecutionRunActive => Volatile.Read(ref runState) == RunStateExecuting;

    /// <summary>True while a Decision Runtime run (seed + propose decisions) is in progress (m5).</summary>
    public bool IsDecisionRunActive => Volatile.Read(ref decisionState) == DecisionStateActive;

    /// <summary>The in-flight planning turn, or a completed task when idle. Lets callers/tests await turn completion.</summary>
    public Task PlanningTurnTask => planningTurn ?? Task.CompletedTask;

    /// <summary>The in-flight Execute Plan run, or a completed task when idle. Lets callers/tests await it (m4).</summary>
    public Task ExecutionRunTask => executionRun ?? Task.CompletedTask;

    /// <summary>The in-flight Decision Runtime run, or a completed task when idle. Lets callers/tests await it (m5).</summary>
    public Task DecisionRunTask => decisionRun ?? Task.CompletedTask;

    /// <summary>Provenance recorded at issuance for the initial plan and every revision (m3).</summary>
    public IReadOnlyList<PromptProvenance> PlanningProvenance => planningProvenance;

    public PromptProvenance? LastPlanningProvenance =>
        planningProvenance.Count > 0 ? planningProvenance[^1] : null;

    /// <summary>Provenance recorded at issuance for the ExtractMilestones and StartExecution turns (m4).</summary>
    public IReadOnlyList<PromptProvenance> ExecutionProvenance => executionProvenance;

    public PromptProvenance? LastExecutionProvenance =>
        executionProvenance.Count > 0 ? executionProvenance[^1] : null;

    /// <summary>Provenance recorded at issuance for the StartDecisionSession seed and GetNextDecisions turns (m5).</summary>
    public IReadOnlyList<PromptProvenance> DecisionProvenance => decisionProvenance;

    public PromptProvenance? LastDecisionProvenance =>
        decisionProvenance.Count > 0 ? decisionProvenance[^1] : null;

    /// <summary>
    /// The flow-specific conversation projection (m6): an ordered, append-only transcript of the loop's turns
    /// (planning, operational output, decision output, submit, continuation). A snapshot copy, safe to enumerate.
    /// </summary>
    public ConversationProjection Conversation
    {
        get
        {
            lock (conversation)
            {
                return new ConversationProjection(conversation.ToArray());
            }
        }
    }

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

        // The single runState gate (claimed here) is what excludes an in-progress execution run — no
        // separate pre-check, which would only re-introduce a TOCTOU window.
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

        // The single runState gate (claimed here) is what excludes an in-progress execution run — no
        // separate pre-check, which would only re-introduce a TOCTOU window.
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
    /// Execute Plan (m4): the bridge from authoring into operational execution. Synchronously it closes
    /// the held-open planning process and launches a background run on the orchestrator's lifetime; the
    /// run copies the plan to operational context, caches it, extracts milestones, commits+pushes the
    /// planning/milestone artifacts, starts execution, and rotates the first handoff — streaming each
    /// step to <see cref="ExecutionStream"/>. This method returns once the run is launched.
    /// </summary>
    public async Task BeginExecutePlanAsync(Repository repository, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        // Claim the repository-wide gate FIRST, before any await — this is what makes planning and
        // execution mutually exclusive (the claim throws if a planning turn holds the gate). Everything
        // after runs under the claim and releases it on any failure path.
        ClaimExecutionRun();
        try
        {
            string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);
            if (!await artifactStore.ExistsAsync(planPath).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Cannot execute: .agents/plan.md does not exist yet.");
            }

            // Execute Plan is a one-way authoring->execution transition. Once a run has rotated a handoff
            // into history, re-executing would re-commit, re-run the operational turns, and overwrite the
            // live handoff — so reject it. A run that FAILED before rotating leaves no historical handoff,
            // so retries after an early-boundary failure still work. (This also makes the in-memory
            // rotation counter safe: a second successful rotation for the same plan can never occur.)
            string handoffsDirectory = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.HandoffsDirectory);
            IReadOnlyList<string> historicalHandoffs = await artifactStore
                .ListAsync(handoffsDirectory, OrchestrationArtifactPaths.HistoricalHandoffSearchPattern)
                .ConfigureAwait(false);
            if (historicalHandoffs.Count > 0)
            {
                throw new InvalidOperationException("This plan has already been executed; start a new plan to execute again.");
            }

            string plan = await artifactStore.ReadAsync(planPath).ConfigureAwait(false) ?? string.Empty;

            // Close the held-open planning process BEFORE the operational one-shot turns: planning is
            // over, and the operational turns must own the workspace alone.
            await ClosePlanningSessionAsync().ConfigureAwait(false);
            await LaunchExecutionRunAsync(repository, plan).ConfigureAwait(false);
        }
        catch
        {
            ReleaseExecutionRun();
            throw;
        }
    }

    /// <summary>
    /// Decision Runtime (m5): make the held-open zero-permission Decision process propose decisions. The run
    /// (on the orchestrator's lifetime) lazily SEEDS the process with <c>StartDecisionSession.Render(operational
    /// context)</c> — a turn deliberately kept OFF the primary stream — then submits <c>GetNextDecisions.Render(
    /// handoff)</c> to the SAME warm process, streaming its output to <see cref="DecisionStream"/> and capturing
    /// it as the proposed decisions. The decisions become editable user content only when the turn completes
    /// (a <c>review-ready</c> frame); nothing is persisted until a human submits. This method returns once the
    /// run is launched. The Decision process has zero operational authority: it never commits, pushes, runs
    /// execution, or writes artifacts — the only persistence is the human-gated <see cref="BeginSubmitDecisionsAsync"/>.
    /// </summary>
    public async Task BeginDecisionRunAsync(Repository repository, DecisionRoute route = DecisionRoute.Continue, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        // Claim the decision gate FIRST, before any await — rejects a second concurrent decision run while
        // leaving planning/execution untouched (decisions run on their own independent gate).
        ClaimDecisionRun();
        try
        {
            // Seeding needs the operational context. Require it up front so a premature run fails fast (409)
            // instead of opening a process and failing mid-stream. (A Transfer reads it too, to reseed from.)
            string operationalContextPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.OperationalContext);
            if (!await artifactStore.ExistsAsync(operationalContextPath).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Cannot propose decisions: .agents/operational_context.md does not exist yet. Execute a plan first.");
            }

            await LaunchDecisionRunAsync(repository, route).ConfigureAwait(false);
        }
        catch
        {
            ReleaseDecisionRun();
            throw;
        }
    }

    /// <summary>
    /// The human review gate + continuation driver (m5 gate, m6 loop): persist the reviewed/edited decisions
    /// the operator submits, then continue operational execution. This is the ONLY way captured decision output
    /// crosses into operational authority. It persists a rotated, numbered submission (<c>decisions.000N.md</c>)
    /// for history/recovery AND rewrites the canonical <c>.agents/decisions/decisions.md</c> every downstream
    /// consumer + the next continuation reads, THEN launches a background <c>ContinueExecution</c> run over the
    /// cached plan, latest handoff, and these decisions (streamed to <see cref="ExecutionStream"/>). The
    /// continuation is an operational execution run, so it claims the repository-wide run gate up front (a 409
    /// if a planning turn or another execution/continuation run holds it) BEFORE any persistence, so a rejected
    /// submit leaves no half-written state. Persistence always completes before the continuation turn runs.
    /// </summary>
    public async Task BeginSubmitDecisionsAsync(Repository repository, string decisions, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(decisions))
        {
            throw new ArgumentException("Decisions text is required to submit decisions.", nameof(decisions));
        }

        ClaimExecutionRun();
        try
        {
            // Persist BEFORE the continuation starts (certification): a numbered submission for recovery plus
            // the live canonical decisions.md the ContinueExecution turn and every downstream consumer read.
            int sequence = await NextDecisionSequenceAsync(repository).ConfigureAwait(false);
            string numberedRelative = OrchestrationArtifactPaths.HistoricalDecision(sequence);
            await artifactStore.WriteAsync(
                ArtifactPath.ResolveRepositoryPath(repository, numberedRelative), decisions).ConfigureAwait(false);
            await artifactStore.WriteAsync(
                ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Decisions), decisions).ConfigureAwait(false);
            RecordDecisions(decisions);
            AdvanceIteration();
            AppendConversation(ConversationEntryKind.Submit, $"Submitted decisions #{sequence}.", numberedRelative);

            // path stays the live canonical path (back-compat with the m5 contract); the rotated submission is
            // additive (numberedPath/sequence) so the UI can show recovery state.
            TryPublishDecision("submitted", new
            {
                path = OrchestrationArtifactPaths.Decisions,
                numberedPath = numberedRelative,
                sequence,
            });

            await LaunchContinuationRunAsync(repository, decisions).ConfigureAwait(false);
        }
        catch
        {
            ReleaseExecutionRun();
            throw;
        }
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
        Task? execution;
        Task? decisionRunDrain;
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
            // Capture under the gate: LaunchPlanningTurnAsync/LaunchExecutionRunAsync/LaunchDecisionRunAsync
            // assign their tasks under the SAME gate with a disposed re-check, so an in-flight run is always
            // seen here, and a run that would start after this point observes disposed==true and never
            // launches (nothing to drain is missed).
            turn = planningTurn;
            execution = executionRun;
            decisionRunDrain = decisionRun;
        }
        finally
        {
            gate.Release();
        }

        // Cancel the lifetime FIRST so the in-flight planning turn / execution run observes cancellation
        // and stops publishing, THEN drain both, and only THEN complete the streams — otherwise a late
        // Publish would hit a completed channel (which fail-fast throws).
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

        if (execution is not null)
        {
            try
            {
                await execution.ConfigureAwait(false);
            }
            catch
            {
                // Best-effort drain during teardown; the run's own failure path already reported it.
            }
        }

        if (decisionRunDrain is not null)
        {
            try
            {
                await decisionRunDrain.ConfigureAwait(false);
            }
            catch
            {
                // Best-effort drain during teardown; the run's own failure path already reported it.
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

    // Closes the held-open decision process (gate-guarded null-then-dispose, so Dispose and this can never
    // both dispose the same handle). The next EnsureDecisionSessionAsync opens a fresh process.
    private async Task CloseDecisionSessionAsync()
    {
        IAgentSession? decision;
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            decision = decisionSession;
            decisionSession = null;
        }
        finally
        {
            gate.Release();
        }

        // The next process is fresh, so its observed pressure starts from zero (m7).
        Interlocked.Exchange(ref decisionSessionTokens, 0);

        if (decision is not null)
        {
            await decision.DisposeAsync().ConfigureAwait(false);
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
            AppendConversation(ConversationEntryKind.Planning, $"Plan authored ({phase}).", OrchestrationArtifactPaths.Plan);
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
        int previous = Interlocked.CompareExchange(ref runState, RunStatePlanning, RunStateIdle);
        if (previous != RunStateIdle)
        {
            throw new InvalidOperationException(previous == RunStateExecuting
                ? "An execution run is in progress; planning is closed for this repository."
                : "A planning turn is already running for this repository.");
        }
    }

    // CompareExchange (not Exchange) so a release only clears the gate it owns — a stray/duplicate release
    // can never wipe a peer's claim.
    private void ReleasePlanningTurn() => Interlocked.CompareExchange(ref runState, RunStateIdle, RunStatePlanning);

    private void ClaimExecutionRun()
    {
        int previous = Interlocked.CompareExchange(ref runState, RunStateExecuting, RunStateIdle);
        if (previous != RunStateIdle)
        {
            throw new InvalidOperationException(previous == RunStatePlanning
                ? "A planning turn is still running; wait for it to complete before executing."
                : "An execution run is already in progress for this repository.");
        }
    }

    private void ReleaseExecutionRun() => Interlocked.CompareExchange(ref runState, RunStateIdle, RunStateExecuting);

    // Non-throwing variant of ClaimExecutionRun: returns whether the execution gate was claimed (false if a
    // planning turn or another execution/continuation run already holds it). The Transfer path uses this to
    // take the gate for its operational workspace-write ONLY when it is free, deferring to warm reuse otherwise.
    private bool TryClaimExecutionRun() =>
        Interlocked.CompareExchange(ref runState, RunStateExecuting, RunStateIdle) == RunStateIdle;

    // Mirrors LaunchPlanningTurnAsync: assigns executionRun + captures the lifetime token UNDER the gate
    // with a fresh disposed re-check, so a run is never launched after DisposeAsync flips disposed, and a
    // run launched before it is always visible to the dispose drain.
    private async Task LaunchExecutionRunAsync(Repository repository, string plan)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            CancellationToken token = lifetimeCts.Token;
            executionRun = Task.Run(() => RunExecutionAsync(repository, plan, token));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RunExecutionAsync(Repository repository, string plan, CancellationToken cancellationToken)
    {
        try
        {
            ExecutionStream.Publish("run-started", Serialize(new { phase = "ExecutePlan" }));

            // Copy the plan into operational context, then cache the plan text under the active-run slot.
            await artifactStore.WriteAsync(
                ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.OperationalContext),
                plan).ConfigureAwait(false);
            RecordPlan(plan);

            // 1) Extract milestones — Operational, ExtraHigh, one-shot. Writes .agents/milestones/m*.md.
            ExecutionStream.Publish("phase", Serialize(new { phase = "ExtractMilestones" }));
            AgentTurnResult extract = await agentRuntime.RunOneShotAsync(
                BuildOperationalSpec(repository, AgentEffortLevel.High, "xhigh"),
                ExtractMilestones.Text,
                chunk => PublishExecutionDelta("ExtractMilestones", chunk),
                cancellationToken).ConfigureAwait(false);
            executionProvenance.Add(BuildExtractMilestonesProvenance());
            if (extract.State != AgentTurnState.Completed)
            {
                ExecutionStream.Publish("failed", Serialize(new
                {
                    phase = "ExtractMilestones",
                    reason = DescribeExecutionFailure(extract.State, "milestone extraction"),
                    detail = extract.Output,
                }));
                return;
            }

            // Verify Codex actually produced milestone files.
            IReadOnlyList<string> milestones = await artifactStore.ListAsync(
                ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.MilestonesDirectory),
                OrchestrationArtifactPaths.MilestoneSearchPattern).ConfigureAwait(false);
            if (milestones.Count == 0)
            {
                ExecutionStream.Publish("failed", Serialize(new
                {
                    phase = "ExtractMilestones",
                    reason = "Milestone extraction completed but no .agents/milestones/m*.md files were produced.",
                }));
                return;
            }

            ExecutionStream.Publish("milestones-extracted", Serialize(new { count = milestones.Count }));

            // 2) Commit + push the planning/milestone artifacts.
            PlanPublicationResult publication = await planArtifactPublisher.PublishAsync(
                repository,
                "Author plan and extract milestones",
                BuildPublicationPaths(repository, milestones),
                cancellationToken).ConfigureAwait(false);
            if (!publication.Succeeded)
            {
                ExecutionStream.Publish("failed", Serialize(new
                {
                    phase = "Publish",
                    reason = $"Committing the plan and milestones failed: {publication.FailureReason}",
                }));
                return;
            }

            ExecutionStream.Publish("committed", Serialize(new { commitSha = publication.CommitSha, pushed = publication.Pushed }));

            // 3) Lifecycle crosses to ExecutingPlan (derived from plan existence; emitted for the UI).
            ExecutionStream.Publish("lifecycle", Serialize(new { state = nameof(PlanLifecycleState.ExecutingPlan) }));

            // 4) Start execution — Operational, Medium, one-shot. Writes the live handoff.
            ExecutionStream.Publish("phase", Serialize(new { phase = "StartExecution" }));
            AgentTurnResult start = await agentRuntime.RunOneShotAsync(
                BuildOperationalSpec(repository, AgentEffortLevel.Medium, identifier: null),
                StartExecution.Render(plan),
                chunk => PublishExecutionDelta("StartExecution", chunk),
                cancellationToken).ConfigureAwait(false);
            executionProvenance.Add(BuildStartExecutionProvenance());
            if (start.State != AgentTurnState.Completed)
            {
                ExecutionStream.Publish("failed", Serialize(new
                {
                    phase = "StartExecution",
                    reason = DescribeExecutionFailure(start.State, "start execution"),
                    detail = start.Output,
                }));
                return;
            }

            // 5) Verify + read the live handoff, then rotate it to handoff.000N.md.
            string livePath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.LiveHandoff);
            if (!await artifactStore.ExistsAsync(livePath).ConfigureAwait(false))
            {
                ExecutionStream.Publish("failed", Serialize(new
                {
                    phase = "StartExecution",
                    reason = "Start execution completed but .agents/handoffs/handoff.md was not written.",
                }));
                return;
            }

            string handoff = await artifactStore.ReadAsync(livePath).ConfigureAwait(false) ?? string.Empty;
            RecordHandoff(handoff);

            int sequence = ++handoffSequence;
            string rotatedRelative = OrchestrationArtifactPaths.HistoricalHandoff(sequence);
            await artifactStore.WriteAsync(
                ArtifactPath.ResolveRepositoryPath(repository, rotatedRelative),
                handoff).ConfigureAwait(false);
            await artifactStore.DeleteAsync(livePath).ConfigureAwait(false);
            ExecutionStream.Publish("handoff-rotated", Serialize(new { sequence, path = rotatedRelative }));
            AppendConversation(ConversationEntryKind.OperationalOutput, $"Start execution produced handoff #{sequence}.", rotatedRelative);

            ExecutionStream.Publish("completed", Serialize(new
            {
                commitSha = publication.CommitSha,
                milestoneCount = milestones.Count,
                handoffPath = rotatedRelative,
                promptTokens = extract.Usage.PromptTokens + start.Usage.PromptTokens,
                outputTokens = extract.Usage.OutputTokens + start.Usage.OutputTokens,
            }));
        }
        catch (OperationCanceledException)
        {
            // Disposing (lifetime cancelled). The stream is about to Complete(); do not publish into it.
        }
        catch (Exception exception)
        {
            TryPublishExecutionFailed(exception.Message);
        }
        finally
        {
            ReleaseExecutionRun();
        }
    }

    private Task PublishExecutionDelta(string phase, AgentStreamChunk chunk)
    {
        if (chunk.Stream == AgentProcessOutputStream.StandardOutput && !string.IsNullOrEmpty(chunk.Content))
        {
            ExecutionStream.Publish("delta", Serialize(new { phase, text = chunk.Content }));
        }

        return Task.CompletedTask;
    }

    private void TryPublishExecutionFailed(string reason)
    {
        try
        {
            ExecutionStream.Publish("failed", Serialize(new { reason }));
        }
        catch (InvalidOperationException)
        {
            // Stream already completed during teardown; nothing left to notify.
        }
    }

    // ---- Continuation Loop (m6) ----

    // Mirrors LaunchExecutionRunAsync: assigns executionRun + captures the lifetime token UNDER the gate with a
    // fresh disposed re-check, so the continuation is never launched after DisposeAsync flips disposed, and one
    // launched before it is always visible to the dispose drain.
    private async Task LaunchContinuationRunAsync(Repository repository, string decisions)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            CancellationToken token = lifetimeCts.Token;
            executionRun = Task.Run(() => RunContinuationAsync(repository, decisions, token));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RunContinuationAsync(Repository repository, string decisions, CancellationToken cancellationToken)
    {
        bool continued = false;
        try
        {
            ExecutionStream.Publish("run-started", Serialize(new { phase = "ContinueExecution" }));

            // The cached plan under the reserved {repositoryId}:Plan slot (m4 cached it at Execute Plan), with a
            // fall back to the live plan artifact so a restarted orchestrator still continues against the plan.
            string plan = await ResolvePlanAsync(repository).ConfigureAwait(false);

            // The latest execution handoff, resolved from DISK (restart-safe): live handoff.md else newest rotated.
            (string? handoff, string? handoffPath) = await ReadLatestHandoffAsync(repository).ConfigureAwait(false);
            if (handoff is null)
            {
                ExecutionStream.Publish("failed", Serialize(new
                {
                    phase = "ContinueExecution",
                    reason = "No execution handoff is available to continue from; execute a plan first.",
                }));
                return;
            }

            // Continue operational execution — Operational, Medium, one-shot. Writes the next live handoff.
            ExecutionStream.Publish("phase", Serialize(new { phase = "ContinueExecution" }));
            AgentTurnResult continuation = await agentRuntime.RunOneShotAsync(
                BuildOperationalSpec(repository, AgentEffortLevel.Medium, identifier: null),
                ContinueExecution.Render(plan, handoff, decisions),
                chunk => PublishExecutionDelta("ContinueExecution", chunk),
                cancellationToken).ConfigureAwait(false);
            executionProvenance.Add(BuildContinueExecutionProvenance(handoffPath!));
            if (continuation.State != AgentTurnState.Completed)
            {
                ExecutionStream.Publish("failed", Serialize(new
                {
                    phase = "ContinueExecution",
                    reason = DescribeExecutionFailure(continuation.State, "continue execution"),
                    detail = continuation.Output,
                }));
                return;
            }

            // Verify + read the new live handoff, then rotate it to the NEXT handoff.000N.md. The sequence is
            // computed from DISK (newest rotated + 1), so the loop survives a restart without clobbering history.
            string livePath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.LiveHandoff);
            if (!await artifactStore.ExistsAsync(livePath).ConfigureAwait(false))
            {
                ExecutionStream.Publish("failed", Serialize(new
                {
                    phase = "ContinueExecution",
                    reason = "Continue execution completed but .agents/handoffs/handoff.md was not written.",
                }));
                return;
            }

            string newHandoff = await artifactStore.ReadAsync(livePath).ConfigureAwait(false) ?? string.Empty;
            RecordHandoff(newHandoff);

            int sequence = await NextHandoffSequenceAsync(repository).ConfigureAwait(false);
            string rotatedRelative = OrchestrationArtifactPaths.HistoricalHandoff(sequence);
            await artifactStore.WriteAsync(
                ArtifactPath.ResolveRepositoryPath(repository, rotatedRelative), newHandoff).ConfigureAwait(false);
            await artifactStore.DeleteAsync(livePath).ConfigureAwait(false);
            ExecutionStream.Publish("handoff-rotated", Serialize(new { sequence, path = rotatedRelative }));
            AppendConversation(ConversationEntryKind.Continuation, $"Continuation produced handoff #{sequence}.", rotatedRelative);

            ExecutionStream.Publish("completed", Serialize(new
            {
                handoffPath = rotatedRelative,
                promptTokens = continuation.Usage.PromptTokens,
                outputTokens = continuation.Usage.OutputTokens,
            }));

            // Observed accounting (m7): fold this continuation's tokens into cumulative operational pressure,
            // the second half of the router's target signal.
            Interlocked.Add(ref operationalSessionTokens, continuation.Usage.PromptTokens + continuation.Usage.OutputTokens);
            continued = true;
        }
        catch (OperationCanceledException)
        {
            // Disposing (lifetime cancelled). The stream is about to Complete(); do not publish into it.
        }
        catch (Exception exception)
        {
            TryPublishExecutionFailed(exception.Message);
        }
        finally
        {
            ReleaseExecutionRun();
        }

        // Router evaluation (m7): after a SUCCESSFUL continuation + handoff rotation, consult the lifecycle
        // router to Continue (reuse the warm Decision process) or Transfer (recycle it), then route the next
        // decision turn so the UI returns to decision streaming without leaving the Plan Authoring screen.
        // Runs on the INDEPENDENT decision gate, AFTER the execution gate is released.
        if (continued && !cancellationToken.IsCancellationRequested)
        {
            await RouteNextDecisionRunAsync(repository).ConfigureAwait(false);
        }
    }

    // The m7 router seam: route the next decision turn on decision-session token pressure — Continue (warm
    // reuse) vs Transfer (recycle) — then launch the run on that route. Best effort — swallows a claim
    // conflict (a decision run is already active) and disposal so a continuation that raced teardown or an
    // already-running decision run never faults the continuation task. The router is pure/synchronous so it
    // cannot throw, but the call is still guarded: a routing fault must never strand the loop, and reuse is
    // the safe default. The Transfer route is additionally gated by eligibility (a primed Decision process).
    private async Task RouteNextDecisionRunAsync(Repository repository)
    {
        DecisionRoute route;
        try
        {
            route = decisionSessionRouter.Evaluate(ComputeRouterInputs());
        }
        catch
        {
            route = DecisionRoute.Continue;
        }

        // Eligibility (m7): only Transfer (recycle) when there is a PRIMED Decision process to extract a delta
        // from. Recycling an unseeded process would extract a delta from an empty conversation and corrupt
        // operational context, so an ineligible Transfer degrades to warm reuse — which then seeds the process.
        if (route == DecisionRoute.Transfer && !decisionSeeded)
        {
            route = DecisionRoute.Continue;
        }

        try
        {
            await BeginDecisionRunAsync(repository, route).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Orchestrator torn down between continuation success and the follow-up launch.
        }
        catch (InvalidOperationException)
        {
            // A decision run is already active, or the operational context vanished — nothing to start.
        }
    }

    // The router's signal (m7): the live Decision process's OBSERVED token pressure where available, else a
    // DETERMINISTIC estimate from the content it reasons over (latest handoff + decisions) — the accepted
    // fallback until observed accounting is certified. Recorded into RouterInputs (the surfaced routing
    // signal) and returned for the route decision.
    private RouterInputs ComputeRouterInputs()
    {
        int observedDecision = Volatile.Read(ref decisionSessionTokens);
        int decisionSignal = observedDecision > 0
            ? observedDecision
            : EstimateTokens(CurrentHandoff) + EstimateTokens(CurrentDecisions);
        var inputs = new RouterInputs(decisionSignal, Volatile.Read(ref operationalSessionTokens));
        RecordRouterInputs(inputs);
        return inputs;
    }

    // Deterministic token estimate (~1 token per 4 characters), matching the runtime's deterministic estimator.
    private static int EstimateTokens(string? text) => string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    // The cached plan per the spec's ContinueExecution.Render(MemoryCache.Get("{repositoryId}:Plan"), ...): the
    // reserved active-run slot first, then the in-process CachedPlan, then the live plan artifact so a restarted
    // orchestrator continues against the real plan rather than an empty string.
    private async Task<string> ResolvePlanAsync(Repository repository)
    {
        if (memoryCache.TryGetValue(OrchestrationCacheKeys.PlanRun(RepositoryId), out ActiveRunSnapshot? snapshot) &&
            snapshot?.Plan is { Length: > 0 } cachedPlan)
        {
            return cachedPlan;
        }

        if (CachedPlan is { Length: > 0 })
        {
            return CachedPlan;
        }

        string planPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.Plan);
        return await artifactStore.ReadAsync(planPath).ConfigureAwait(false) ?? string.Empty;
    }

    // Next rotated decision number = (highest existing decisions.000N.md) + 1, computed from DISK so a restarted
    // orchestrator (in-memory state lost) continues the sequence rather than clobbering decisions.0001.md. This
    // is the recovery anchor for "latest persisted decision sequence".
    private async Task<int> NextDecisionSequenceAsync(Repository repository)
    {
        IReadOnlyList<string> existing = await artifactStore.ListAsync(
            ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.DecisionsDirectory),
            OrchestrationArtifactPaths.HistoricalDecisionSearchPattern).ConfigureAwait(false);
        return HighestSequence(existing) + 1;
    }

    // Next rotated handoff number = (highest existing handoff.000N.md) + 1, from DISK (restart-safe). m4's
    // first rotation uses an in-memory counter under its one-way re-execution guard; the continuation loop must
    // read disk so it never clobbers the history the previous run (or a previous process) already rotated.
    private async Task<int> NextHandoffSequenceAsync(Repository repository)
    {
        IReadOnlyList<string> existing = await artifactStore.ListAsync(
            ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.HandoffsDirectory),
            OrchestrationArtifactPaths.HistoricalHandoffSearchPattern).ConfigureAwait(false);
        return HighestSequence(existing) + 1;
    }

    // Parses the zero-padded 4-digit sequence out of each rotated file name (handoff.0007.md / decisions.0007.md
    // -> 7) and returns the max, or 0 when none exist. The penultimate dot-segment is the number for both schemes.
    private static int HighestSequence(IReadOnlyList<string> rotatedPaths)
    {
        int highest = 0;
        foreach (string path in rotatedPaths)
        {
            string fileName = Path.GetFileName(path);
            string[] parts = fileName.Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[^2], out int sequence) && sequence > highest)
            {
                highest = sequence;
            }
        }

        return highest;
    }

    private static PromptProvenance BuildContinueExecutionProvenance(string handoffPath) =>
        new()
        {
            PromptName = nameof(ContinueExecution),
            PromptType = typeof(ContinueExecution).FullName!,
            SourceHash = ContinueExecution.SourceHash,
            SessionRole = PromptSessionRole.OperationalExecution,
            WorkflowPhase = "ContinueExecution",
            InputArtifactIdentities = new[] { OrchestrationArtifactPaths.Plan, handoffPath, OrchestrationArtifactPaths.Decisions },
            OutputArtifactIdentities = new[] { OrchestrationArtifactPaths.LiveHandoff },
        };

    private void AppendConversation(ConversationEntryKind kind, string summary, string? reference)
    {
        lock (conversation)
        {
            conversation.Add(new ConversationEntry(++conversationSequence, kind, IterationCounter, summary, reference));
        }
    }

    // ---- Decision Runtime (m5) ----

    private void ClaimDecisionRun()
    {
        int previous = Interlocked.CompareExchange(ref decisionState, DecisionStateActive, DecisionStateIdle);
        if (previous != DecisionStateIdle)
        {
            throw new InvalidOperationException("A decision run is already in progress for this repository.");
        }
    }

    // CompareExchange (not Exchange) so a release only clears the gate it owns — a stray/duplicate release
    // can never wipe a peer's claim.
    private void ReleaseDecisionRun() => Interlocked.CompareExchange(ref decisionState, DecisionStateIdle, DecisionStateActive);

    // Mirrors LaunchExecutionRunAsync: assigns decisionRun + captures the lifetime token UNDER the gate with a
    // fresh disposed re-check, so a run is never launched after DisposeAsync flips disposed, and a run launched
    // before it is always visible to the dispose drain.
    private async Task LaunchDecisionRunAsync(Repository repository, DecisionRoute route)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            CancellationToken token = lifetimeCts.Token;
            decisionRun = Task.Run(() => RunDecisionAsync(repository, route, token));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RunDecisionAsync(Repository repository, DecisionRoute route, CancellationToken cancellationToken)
    {
        try
        {
            // A Transfer recycles the Decision process BEFORE proposing: extract an operational delta, rewrite
            // operational context, close the old process, and seed a FRESH one from the rewritten context. The
            // rewrite is an operational WORKSPACE WRITE, so the transfer claims the execution gate for its
            // duration to stay mutually exclusive with a concurrent continuation (which also writes the
            // workspace). If a continuation already holds the gate, the recycle is DEFERRED to warm reuse this
            // round (Transfer retries on the next continuation). The effective route is resolved BEFORE the
            // run-started frame so the stream announces the route that actually runs (a deferred Transfer reads
            // as Continue), not the verdict. The gate is held across PrepareTransferAsync and released before the
            // read-only proposal.
            bool transferring = route == DecisionRoute.Transfer && TryClaimExecutionRun();
            try
            {
                DecisionStream.Publish("run-started", Serialize(new
                {
                    phase = "DecisionRun",
                    route = (transferring ? DecisionRoute.Transfer : DecisionRoute.Continue).ToString(),
                }));

                // On success PrepareTransferAsync leaves a freshly-seeded process (decisionSeeded == true), so the
                // seed-once block below is skipped and the proposal runs against the new process. On failure it has
                // published a failed frame and torn down any half-seeded process, so the run ends here.
                if (transferring && !await PrepareTransferAsync(repository, cancellationToken).ConfigureAwait(false))
                {
                    return;
                }
            }
            finally
            {
                if (transferring)
                {
                    ReleaseExecutionRun();
                }
            }

            IAgentSession session = await EnsureDecisionSessionAsync(repository, cancellationToken).ConfigureAwait(false);

            // 1) Seed the held-open process ONCE with the operational context. The seed turn primes the
            // session and is deliberately kept OFF the primary stream (its deltas are dropped); only its
            // success/failure matters here.
            if (!decisionSeeded)
            {
                string operationalContext = await artifactStore.ReadAsync(
                    ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.OperationalContext))
                    .ConfigureAwait(false) ?? string.Empty;

                AgentTurnResult seed = await session.RunTurnAsync(
                    StartDecisionSession.Render(operationalContext),
                    onChunk: null,
                    cancellationToken).ConfigureAwait(false);
                decisionProvenance.Add(BuildStartDecisionSessionProvenance());
                if (seed.State != AgentTurnState.Completed)
                {
                    DecisionStream.Publish("failed", Serialize(new
                    {
                        phase = "StartDecisionSession",
                        reason = DescribeDecisionFailure(seed.State, "decision seeding"),
                        detail = seed.Output,
                    }));

                    // The seed failed, so the held-open process is in an unknown state with a half-primed
                    // conversation. Tear it down (decisionSeeded stays false) so the NEXT run opens a FRESH
                    // process and re-seeds cleanly — rather than submitting a second seed into a poisoned
                    // conversation. This keeps the "seed the held-open process ONCE" invariant true per session.
                    await CloseDecisionSessionAsync().ConfigureAwait(false);
                    return;
                }

                decisionSeeded = true;
            }

            // The validated, logged sandbox posture of the Decision process (zero operational authority).
            DecisionStream.Publish("diagnostics", Serialize(new
            {
                sandbox = "read-only",
                approvals = "never",
                seeded = decisionSeeded,
            }));

            // 2) Resolve the latest execution handoff to reason over. None yet -> nothing to decide.
            (string? handoff, string? handoffPath) = await ReadLatestHandoffAsync(repository).ConfigureAwait(false);
            if (handoff is null)
            {
                DecisionStream.Publish("failed", Serialize(new
                {
                    phase = "GetNextDecisions",
                    reason = "No execution handoff is available yet; run execution before proposing decisions.",
                }));
                return;
            }

            // 3) Propose decisions — streamed to the primary decision stream + captured as editable content.
            DecisionStream.Publish("phase", Serialize(new { phase = "GetNextDecisions" }));
            AgentTurnResult proposed = await session.RunTurnAsync(
                GetNextDecisions.Render(handoff),
                PublishDecisionDelta,
                cancellationToken).ConfigureAwait(false);
            decisionProvenance.Add(BuildGetNextDecisionsProvenance(handoffPath!));
            if (proposed.State != AgentTurnState.Completed)
            {
                DecisionStream.Publish("failed", Serialize(new
                {
                    phase = "GetNextDecisions",
                    reason = DescribeDecisionFailure(proposed.State, "decision proposal"),
                    detail = proposed.Output,
                }));
                return;
            }

            // Capture the proposed decisions in transient run state (NOT persisted — that needs a human submit).
            string decisions = proposed.Output;
            RecordDecisions(decisions);

            // Observed accounting (m7): fold this proposal's tokens into the live decision process's pressure
            // and surface the router's target signal. (decisionSessionTokens resets when the process recycles.)
            Interlocked.Add(ref decisionSessionTokens, proposed.Usage.PromptTokens + proposed.Usage.OutputTokens);
            RecordRouterInputs(new RouterInputs(
                Volatile.Read(ref decisionSessionTokens),
                Volatile.Read(ref operationalSessionTokens)));

            DecisionStream.Publish("completed", Serialize(new
            {
                promptTokens = proposed.Usage.PromptTokens,
                outputTokens = proposed.Usage.OutputTokens,
            }));

            // The output becomes editable user content ONLY now, after the turn completed (review gate).
            AppendConversation(ConversationEntryKind.DecisionOutput, "Decisions proposed and ready for review.", handoffPath);
            DecisionStream.Publish("review-ready", Serialize(new { decisions }));
        }
        catch (OperationCanceledException)
        {
            // Disposing (lifetime cancelled). The stream is about to Complete(); do not publish into it.
        }
        catch (Exception exception)
        {
            TryPublishDecisionFailed(exception.Message);
        }
        finally
        {
            ReleaseDecisionRun();
        }
    }

    // ---- Transfer (m7): recycle the warm Decision process into a fresh one seeded from rewritten context ----

    // The explicit Transfer sequence the lifecycle router selects when Decision-session pressure outweighs reuse.
    // It runs INSIDE the decision run (on the decision gate) BEFORE the proposal, so on success it leaves a
    // freshly-seeded process and the proposal proceeds against it. The phase markers stream so the UI can show
    // the transfer is in progress, but the delta/rewrite/seed turn bodies are kept OFF the primary stream (only
    // their captured output matters). Returns true when the fresh process is seeded and ready to propose, false
    // when any step failed (a failed frame is already published and any half-built process is torn down).
    private async Task<bool> PrepareTransferAsync(Repository repository, CancellationToken cancellationToken)
    {
        // 1) Extract the operational delta from the WARM Decision process and persist it.
        DecisionStream.Publish("phase", Serialize(new { phase = "ProduceOperationalDelta" }));
        IAgentSession warm = await EnsureDecisionSessionAsync(repository, cancellationToken).ConfigureAwait(false);
        AgentTurnResult delta = await warm.RunTurnAsync(
            ProduceOperationalDelta.Text, onChunk: null, cancellationToken).ConfigureAwait(false);
        decisionProvenance.Add(BuildProduceOperationalDeltaProvenance());
        if (delta.State != AgentTurnState.Completed)
        {
            DecisionStream.Publish("failed", Serialize(new
            {
                phase = "ProduceOperationalDelta",
                reason = DescribeDecisionFailure(delta.State, "operational delta extraction"),
                detail = delta.Output,
            }));
            return false;
        }

        await artifactStore.WriteAsync(
            ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.OperationalDelta),
            delta.Output).ConfigureAwait(false);

        // 2) Close the old Decision process — the transfer recycles it. (Resets observed pressure to zero.)
        await CloseDecisionSessionAsync().ConfigureAwait(false);
        decisionSeeded = false;

        // 3) Fold the delta into the next operational context revision — Operational, ExtraHigh, one-shot. The
        // turn reads operational_context.md + operational_delta.md from the workspace and rewrites the context.
        DecisionStream.Publish("phase", Serialize(new { phase = "UpdateOperationalContext" }));
        AgentTurnResult rewrite = await agentRuntime.RunOneShotAsync(
            BuildOperationalSpec(repository, AgentEffortLevel.High, "xhigh"),
            UpdateOperationalContext.Text,
            onChunk: null,
            cancellationToken).ConfigureAwait(false);
        decisionProvenance.Add(BuildUpdateOperationalContextProvenance());
        if (rewrite.State != AgentTurnState.Completed)
        {
            DecisionStream.Publish("failed", Serialize(new
            {
                phase = "UpdateOperationalContext",
                reason = DescribeDecisionFailure(rewrite.State, "operational context rewrite"),
                detail = rewrite.Output,
            }));
            return false;
        }

        string contextPath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.OperationalContext);
        if (!await artifactStore.ExistsAsync(contextPath).ConfigureAwait(false))
        {
            DecisionStream.Publish("failed", Serialize(new
            {
                phase = "UpdateOperationalContext",
                reason = "Transfer left no .agents/operational_context.md to seed the next decision session from.",
            }));
            return false;
        }

        string newContext = await artifactStore.ReadAsync(contextPath).ConfigureAwait(false) ?? string.Empty;

        // 4) Open a FRESH Decision process and seed it from the rewritten operational context.
        DecisionStream.Publish("phase", Serialize(new { phase = "StartDecisionSessionFromTransfer" }));
        IAgentSession fresh = await EnsureDecisionSessionAsync(repository, cancellationToken).ConfigureAwait(false);
        AgentTurnResult seed = await fresh.RunTurnAsync(
            StartDecisionSessionFromTransfer.Render(newContext), onChunk: null, cancellationToken).ConfigureAwait(false);
        decisionProvenance.Add(BuildStartDecisionSessionFromTransferProvenance());
        if (seed.State != AgentTurnState.Completed)
        {
            DecisionStream.Publish("failed", Serialize(new
            {
                phase = "StartDecisionSessionFromTransfer",
                reason = DescribeDecisionFailure(seed.State, "transfer reseed"),
                detail = seed.Output,
            }));

            // Tear down the half-seeded fresh process so the next run opens cleanly (process cleanup).
            await CloseDecisionSessionAsync().ConfigureAwait(false);
            return false;
        }

        decisionSeeded = true;
        DecisionStream.Publish("transferred", Serialize(new
        {
            operationalDelta = OrchestrationArtifactPaths.OperationalDelta,
            operationalContext = OrchestrationArtifactPaths.OperationalContext,
        }));
        return true;
    }

    private static PromptProvenance BuildProduceOperationalDeltaProvenance() =>
        new()
        {
            PromptName = nameof(ProduceOperationalDelta),
            PromptType = typeof(ProduceOperationalDelta).FullName!,
            SourceHash = ProduceOperationalDelta.SourceHash,
            SessionRole = PromptSessionRole.Transfer,
            WorkflowPhase = "ProduceOperationalDelta",
            // No input identities: the prompt renders no files — it extracts the delta from the in-process
            // Decision conversation, not from operational_context.md. The delta artifact is its only output.
            InputArtifactIdentities = Array.Empty<string>(),
            OutputArtifactIdentities = new[] { OrchestrationArtifactPaths.OperationalDelta },
        };

    private static PromptProvenance BuildUpdateOperationalContextProvenance() =>
        new()
        {
            PromptName = nameof(UpdateOperationalContext),
            PromptType = typeof(UpdateOperationalContext).FullName!,
            SourceHash = UpdateOperationalContext.SourceHash,
            SessionRole = PromptSessionRole.ContextUpdate,
            WorkflowPhase = "UpdateOperationalContext",
            InputArtifactIdentities = new[] { OrchestrationArtifactPaths.OperationalContext, OrchestrationArtifactPaths.OperationalDelta },
            OutputArtifactIdentities = new[] { OrchestrationArtifactPaths.OperationalContext },
        };

    private static PromptProvenance BuildStartDecisionSessionFromTransferProvenance() =>
        new()
        {
            PromptName = nameof(StartDecisionSessionFromTransfer),
            PromptType = typeof(StartDecisionSessionFromTransfer).FullName!,
            SourceHash = StartDecisionSessionFromTransfer.SourceHash,
            SessionRole = PromptSessionRole.Transfer,
            WorkflowPhase = "StartDecisionSessionFromTransfer",
            InputArtifactIdentities = new[] { OrchestrationArtifactPaths.OperationalContext },
            OutputArtifactIdentities = Array.Empty<string>(),
        };

    private Task PublishDecisionDelta(AgentStreamChunk chunk)
    {
        if (chunk.Stream == AgentProcessOutputStream.StandardOutput && !string.IsNullOrEmpty(chunk.Content))
        {
            DecisionStream.Publish("delta", Serialize(new { text = chunk.Content }));
        }

        return Task.CompletedTask;
    }

    // Reads the most recent execution handoff from DISK (restart-safe): the live handoff.md if it is still
    // present, otherwise the highest-numbered rotated handoff.000N.md. Returns (content, repository-relative
    // path) or (null, null) when none exists yet.
    private async Task<(string? Handoff, string? Path)> ReadLatestHandoffAsync(Repository repository)
    {
        string livePath = ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.LiveHandoff);
        if (await artifactStore.ExistsAsync(livePath).ConfigureAwait(false))
        {
            string live = await artifactStore.ReadAsync(livePath).ConfigureAwait(false) ?? string.Empty;
            return (live, OrchestrationArtifactPaths.LiveHandoff);
        }

        IReadOnlyList<string> historical = await artifactStore.ListAsync(
            ArtifactPath.ResolveRepositoryPath(repository, OrchestrationArtifactPaths.HandoffsDirectory),
            OrchestrationArtifactPaths.HistoricalHandoffSearchPattern).ConfigureAwait(false);
        if (historical.Count == 0)
        {
            return (null, null);
        }

        // The zero-padded 4-digit suffix makes ordinal max == newest.
        string newestFullPath = historical.Max(StringComparer.Ordinal)!;
        string content = await artifactStore.ReadAsync(newestFullPath).ConfigureAwait(false) ?? string.Empty;
        return (content, ArtifactPath.ToRepositoryRelativePath(repository, newestFullPath));
    }

    private void TryPublishDecision<T>(string type, T data)
    {
        try
        {
            DecisionStream.Publish(type, Serialize(data));
        }
        catch (InvalidOperationException)
        {
            // Stream already completed during teardown; nothing left to notify.
        }
    }

    private void TryPublishDecisionFailed(string reason)
    {
        try
        {
            DecisionStream.Publish("failed", Serialize(new { reason }));
        }
        catch (InvalidOperationException)
        {
            // Stream already completed during teardown; nothing left to notify.
        }
    }

    // Phase-aware failure sentence for the DECISION stream — never the raw enum token.
    private static string DescribeDecisionFailure(AgentTurnState state, string activity) => state switch
    {
        AgentTurnState.Failed => $"The {activity} run failed.",
        AgentTurnState.Canceled => $"The {activity} run was cancelled.",
        _ => $"The {activity} run ended in an unexpected state ({state}).",
    };

    private static PromptProvenance BuildStartDecisionSessionProvenance() =>
        new()
        {
            PromptName = nameof(StartDecisionSession),
            PromptType = typeof(StartDecisionSession).FullName!,
            SourceHash = StartDecisionSession.SourceHash,
            SessionRole = PromptSessionRole.Decision,
            WorkflowPhase = "StartDecisionSession",
            InputArtifactIdentities = new[] { OrchestrationArtifactPaths.OperationalContext },
            OutputArtifactIdentities = Array.Empty<string>(),
        };

    private static PromptProvenance BuildGetNextDecisionsProvenance(string handoffPath) =>
        new()
        {
            PromptName = nameof(GetNextDecisions),
            PromptType = typeof(GetNextDecisions).FullName!,
            SourceHash = GetNextDecisions.SourceHash,
            SessionRole = PromptSessionRole.Decision,
            WorkflowPhase = "GetNextDecisions",
            InputArtifactIdentities = new[] { handoffPath },
            OutputArtifactIdentities = new[] { OrchestrationArtifactPaths.Decisions },
        };

    // The planning + milestone artifacts to commit. The well-known relative paths are constants; milestone
    // files (full paths from the store) are converted to repository-relative for staging.
    private IReadOnlyList<string> BuildPublicationPaths(Repository repository, IReadOnlyList<string> milestoneFullPaths)
    {
        var paths = new List<string>
        {
            OrchestrationArtifactPaths.Plan,
            OrchestrationArtifactPaths.SpecsRoadmap,
            OrchestrationArtifactPaths.OperationalContext,
        };

        foreach (string milestoneFullPath in milestoneFullPaths)
        {
            paths.Add(ArtifactPath.ToRepositoryRelativePath(repository, milestoneFullPath));
        }

        return paths;
    }

    // Operational one-shot profile: writes the workspace, approvals off, effort by tier (xhigh for
    // milestone extraction, medium for start execution).
    private AgentSessionSpec BuildOperationalSpec(Repository repository, AgentEffortLevel level, string? identifier) =>
        new(
            SessionIdentity.New(),
            RepositoryId,
            SessionRole.OperationalExecution,
            new SandboxProfile("workspace-write", CanWriteWorkspace: true, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(level, Identifier: identifier),
            repository.Path);

    private static PromptProvenance BuildExtractMilestonesProvenance() =>
        new()
        {
            PromptName = nameof(ExtractMilestones),
            PromptType = typeof(ExtractMilestones).FullName!,
            SourceHash = ExtractMilestones.SourceHash,
            SessionRole = PromptSessionRole.OperationalExecution,
            WorkflowPhase = "ExtractMilestones",
            InputArtifactIdentities = new[] { OrchestrationArtifactPaths.Plan, OrchestrationArtifactPaths.OperationalContext },
            OutputArtifactIdentities = new[] { OrchestrationArtifactPaths.MilestonesDirectory },
        };

    private static PromptProvenance BuildStartExecutionProvenance() =>
        new()
        {
            PromptName = nameof(StartExecution),
            PromptType = typeof(StartExecution).FullName!,
            SourceHash = StartExecution.SourceHash,
            SessionRole = PromptSessionRole.OperationalExecution,
            WorkflowPhase = "StartExecution",
            InputArtifactIdentities = new[] { OrchestrationArtifactPaths.OperationalContext },
            OutputArtifactIdentities = new[] { OrchestrationArtifactPaths.LiveHandoff },
        };

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

    // Phase-aware failure sentence for the EXECUTION stream — DescribeTerminalState's wording is
    // planning-specific, so the operational turns get an activity-labeled sentence (e.g. "milestone
    // extraction", "start execution") rather than mislabeling a failed operational turn as planning.
    private static string DescribeExecutionFailure(AgentTurnState state, string activity) => state switch
    {
        AgentTurnState.Failed => $"The {activity} run failed.",
        AgentTurnState.Canceled => $"The {activity} run was cancelled.",
        _ => $"The {activity} run ended in an unexpected state ({state}).",
    };

    private void TouchRunCache() =>
        memoryCache.Set(
            OrchestrationCacheKeys.PlanRun(RepositoryId),
            new ActiveRunSnapshot(IterationCounter, CachedPlan is not null, CachedPlan));

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
