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

    // Serializes session open/close and the dispose handoff so an in-flight EnsureSession can never
    // leak a handle past disposal. NOT disposed (avoids a Release-vs-Dispose ObjectDisposedException).
    private readonly SemaphoreSlim gate = new(1, 1);

    // Cancelled on disposal. The background planning turn and execution run run on THIS token, never on
    // an HTTP request token — so completing the write/revise/execute POST does not abort the streaming run.
    private readonly CancellationTokenSource lifetimeCts = new();

    private readonly List<PromptProvenance> planningProvenance = new();
    private readonly List<PromptProvenance> executionProvenance = new();

    private IAgentSession? planningSession;
    private IAgentSession? decisionSession;

    // The single in-flight planning turn (write or revise). Read by DisposeAsync to drain before
    // completing the stream; written by LaunchPlanningTurn. Volatile for cross-thread visibility.
    private volatile Task? planningTurn;

    // The single in-flight Execute Plan run (m4). Same drain-before-complete contract as planningTurn.
    private volatile Task? executionRun;

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

    private volatile bool disposed;

    public RepositoryOrchestrator(
        string repositoryId,
        IAgentRuntime agentRuntime,
        IArtifactStore artifactStore,
        IMemoryCache memoryCache,
        IPlanArtifactPublisher planArtifactPublisher)
    {
        RepositoryId = repositoryId;
        this.agentRuntime = agentRuntime;
        this.artifactStore = artifactStore;
        this.memoryCache = memoryCache;
        this.planArtifactPublisher = planArtifactPublisher;
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

    /// <summary>The in-flight planning turn, or a completed task when idle. Lets callers/tests await turn completion.</summary>
    public Task PlanningTurnTask => planningTurn ?? Task.CompletedTask;

    /// <summary>The in-flight Execute Plan run, or a completed task when idle. Lets callers/tests await it (m4).</summary>
    public Task ExecutionRunTask => executionRun ?? Task.CompletedTask;

    /// <summary>Provenance recorded at issuance for the initial plan and every revision (m3).</summary>
    public IReadOnlyList<PromptProvenance> PlanningProvenance => planningProvenance;

    public PromptProvenance? LastPlanningProvenance =>
        planningProvenance.Count > 0 ? planningProvenance[^1] : null;

    /// <summary>Provenance recorded at issuance for the ExtractMilestones and StartExecution turns (m4).</summary>
    public IReadOnlyList<PromptProvenance> ExecutionProvenance => executionProvenance;

    public PromptProvenance? LastExecutionProvenance =>
        executionProvenance.Count > 0 ? executionProvenance[^1] : null;

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
            // Capture under the gate: LaunchPlanningTurnAsync/LaunchExecutionRunAsync assign their tasks
            // under the SAME gate with a disposed re-check, so an in-flight run is always seen here, and a
            // run that would start after this point observes disposed==true and never launches (nothing to
            // drain is missed).
            turn = planningTurn;
            execution = executionRun;
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
