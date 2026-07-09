using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Cli.Services.Agents;
using LoopRelay.Cli.Services.Console;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Primitives;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Projections.Abstractions;
using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Projections.Primitives;

namespace LoopRelay.Cli.Services.Decisions;

/// <summary>
/// The decision-making codex session, routed by the SessionRouter. Mirrors RepositoryOrchestrator's
/// RunDecisionAsync + the auto-submit half of BeginSubmitDecisionsAsync (a fully-automated CLI submits
/// the agent's proposal verbatim). Owns ONE warm read-only process reused across iterations. A FRESH process
/// (first pass, or the recycle after a Transfer) is primed with the operational context inline in its first
/// proposal turn — there is NO separate seed turn (the legacy StartDecisionSession* overseer seed, which framed
/// the agent as an executor "waiting for the first session report", is not used in this loop). It proposes the
/// execution agent's system prompt — GenerateSystemPromptForFirstExecutionAgent on the first pass (no handoff
/// yet), else GenerateSystemPromptForNextExecutionAgent(latestHandoff) (a post-Transfer process still has a
/// handoff, so it is a NEXT proposal, not a first) — persists the proposal to decisions.{N:0000}.md AND
/// canonical decisions.md; verifies decisions.md exists. Across CLI runs, the warm process is resumable: the
/// codex thread id + router accounting persist to the runtime SQLite database after every successful proposal
/// (see OpenOrResumeSessionAsync).
/// </summary>
internal sealed class DecisionSession(
    IAgentRuntime _runtime,
    IDecisionSessionRouter _router,
    LoopArtifacts _artifacts,
    ILoopConsole _console,
    Repository _repository,
    IDecisionCostModel? _costModel = null,
    IDecisionSessionResumeStore? _resumeStore = null,
    IProjectContextProjectionService? _projectionService = null,
    bool _resumeEnabled = true,
    string? _promptPolicy = null,
    ExplicitHitlNonImplementationRequestCaptureService? _hitlRequestCapture = null) : IAsyncDisposable
{
    private IAgentSession? session;
    private bool seeded;
    private bool resumeAttempted;

    // Operational-context size-health state (Stage 2, mirrors RepositoryOrchestrator). Single-threaded, no lock.
    private const int OperationalContextGrowthStreakWarningThreshold = 2;
    private int? previousOperationalContextSize;
    private int operationalContextGrowthStreak;

    // Cost-aware routing accounting (mirrors RepositoryOrchestrator). PER-PROCESS fields reset on recycle;
    // transferCost persists across recycles. Single-threaded — RunAsync is called sequentially — so no lock.
    private int occupancyTokens;            // O
    private double reuseCost;               // R
    private int reuseCycles;                // n
    private double lastCycleCost;           // e_last
    private double prevCycleCost;           // e_prev
    private double transferCost = 250_000d; // C: seed -> measured -> running average
    private int transferCount;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        DecisionRoute route = _router.Evaluate(BuildRouterInputs());
        // Eligibility downgrade: a Transfer needs a primed warm process to extract a delta from.
        if (route == DecisionRoute.Transfer && !seeded)
        {
            route = DecisionRoute.Continue;
        }

        _console.Phase($"Decision (route={route})");

        if (route == DecisionRoute.Transfer)
        {
            await TransferAsync(cancellationToken);
        }

        session ??= await OpenOrResumeSessionAsync(cancellationToken);

        (string? handoff, _) = await _artifacts.ReadLatestHandoffAsync();
        string proposalPrompt = await BuildProposalPromptAsync(handoff, cancellationToken);

        // Own phase header so post-transfer proposal output no longer prints under the last
        // "Decision: Transfer/…" header.
        _console.Phase("Decision: Propose");
        var proposalRenderer = new ConsoleTurnRenderer(_console);
        AgentTurnResult proposed = await session.RunTurnAsync(
            proposalPrompt, proposalRenderer.Stream, cancellationToken);

        if (proposed.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException(WithDiagnostics(
                $"Decision turn ended in state {proposed.State}.", proposed.Diagnostics));
        }

        // The process now holds the operational context (delivered inline in this turn), so subsequent proposals
        // on it are cheap handoff-only deltas.
        seeded = true;
        RecordProposalCost(proposed.Usage);
        await PersistResumeStateAsync(cancellationToken);
        proposalRenderer.EchoIfSilent(proposed.Output);

        // Auto-submit: the CLI is fully automated, so the agent's proposal is persisted verbatim.
        await _artifacts.PersistDecisionsAsync(proposed.Output);
        if (_hitlRequestCapture is not null)
        {
            await _hitlRequestCapture.CaptureFromSourceAsync(OrchestrationArtifactPaths.Decisions, proposed.Output);
        }

        if (!await _artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions))
        {
            throw new LoopStepException(".agents/decisions/decisions.md was not written.");
        }

        _console.Info("New decisions.md verified.");
    }

    // decisions.md IS the execution agent's system prompt. The first pass (no prior handoff) authors the FIRST
    // agent's system prompt; any pass with a handoff (including the first proposal on a post-Transfer process)
    // authors the NEXT agent's, folding in the previous session's handoff. A FRESH process is also primed with
    // the operational context in this same turn (there is no separate seed) — a WARM process already carries it
    // from its first proposal, so its later proposals send only the handoff delta.
    private async Task<string> BuildProposalPromptAsync(string? handoff, CancellationToken cancellationToken)
    {
        string decisionSessionProjection = seeded
            ? string.Empty
            : (await EnsureDecisionProjectionAsync(cancellationToken)).Content;
        string baseline = handoff is null
            ? GenerateSystemPromptForFirstExecutionAgent.Render(decisionSessionProjection)
            : GenerateSystemPromptForNextExecutionAgent.Render(decisionSessionProjection, handoff);
        baseline = ImplementationFirstPromptPolicyComposer.AppendPromptPolicy(baseline, (_promptPolicy ?? ImplementationFirstPromptPolicyComposer.ComposeDefault()));

        if (seeded)
        {
            return baseline; // warm process: the projection and operational context are already in this process's history
        }

        await _artifacts.EnsureOperationalContextAsync();
        string operationalContext = await _artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(operationalContext))
        {
            _console.Warn("Operational context is empty — the decision agent has no context to work from.");
        }

        return $"{operationalContext}\n\n{baseline}";
    }

    /// <summary>
    /// The FIRST open of this CLI process attempts to resume the persisted decision session (if any); every
    /// later open — the post-Transfer recycle, the reopen after a failed turn — starts fresh, because the
    /// persisted state describes a thread this process has already moved past. Restored accounting is applied
    /// only HERE, at a successful resume: the router's route evaluation runs before the open, so the first
    /// route of a run always sees pre-restore (zeroed) inputs and the existing !seeded downgrade guards it.
    /// </summary>
    private async Task<IAgentSession> OpenOrResumeSessionAsync(CancellationToken cancellationToken)
    {
        bool firstOpen = !resumeAttempted;
        resumeAttempted = true;

        DecisionSessionResumeState? state = firstOpen && _resumeEnabled
            ? await (_resumeStore ?? new NullDecisionSessionResumeStore()).ReadAsync(cancellationToken)
            : null;
        if (state is not null && _projectionService is not null)
        {
            ProjectionFreshness freshness = await EvaluateDecisionProjectionFreshnessAsync(cancellationToken);
            if (!freshness.IsFresh)
            {
                _console.Warn(
                    "Decision session projection is stale or missing; clearing persisted decision session and starting fresh.");
                await (_resumeStore ?? new NullDecisionSessionResumeStore()).ClearAsync(cancellationToken);
                state = null;
            }
        }

        if (state is null)
        {
            return await _runtime.OpenSessionAsync(AgentSpecs.Decision(_repository), cancellationToken);
        }

        try
        {
            IAgentSession resumed = await _runtime.OpenSessionAsync(
                AgentSpecs.Decision(_repository, state.ThreadId), cancellationToken);

            // The resumed thread already holds the operational context (its first proposal primed it), and
            // the router accounting it accrued — restore both so priming and transfer economics continue
            // where the previous run left off.
            seeded = true;
            occupancyTokens = state.OccupancyTokens;
            reuseCost = state.ReuseCost;
            reuseCycles = state.ReuseCycles;
            lastCycleCost = state.LastCycleCost;
            prevCycleCost = state.PrevCycleCost;
            transferCost = state.TransferCost;
            transferCount = state.TransferCount;
            previousOperationalContextSize = state.PreviousOperationalContextSize;
            operationalContextGrowthStreak = state.OperationalContextGrowthStreak;
            _console.Info($"Resumed decision session (thread {state.ThreadId}).");
            return resumed;
        }
        catch (AgentSessionResumeException ex)
        {
            _console.Warn($"Could not resume decision session (thread {state.ThreadId}): {ex.Message} Starting fresh.");
            await (_resumeStore ?? new NullDecisionSessionResumeStore()).ClearAsync(cancellationToken);
            return await _runtime.OpenSessionAsync(AgentSpecs.Decision(_repository), cancellationToken);
        }
    }

    /// <summary>
    /// The state is only ever written after a SUCCESSFUL proposal turn, so its existence implies the thread is
    /// primed (no seeded field in the schema). One small SQLite upsert per decision step; the store is fail-open.
    /// </summary>
    private async Task PersistResumeStateAsync(CancellationToken cancellationToken)
    {
        if (session?.ThreadId is not { Length: > 0 } threadId)
        {
            return; // no codex thread id (legacy/one-shot shapes) — nothing a later run could resume
        }

        await (_resumeStore ?? new NullDecisionSessionResumeStore()).WriteAsync(new DecisionSessionResumeState(
            threadId, occupancyTokens, reuseCost, reuseCycles, lastCycleCost, prevCycleCost,
            transferCost, transferCount, previousOperationalContextSize, operationalContextGrowthStreak),
            cancellationToken);
    }

    /// <summary>
    /// Transfer recycle, mirroring RepositoryOrchestrator.PrepareTransferAsync: extract an operational delta
    /// from the warm process, close it, rewrite operational_context.md via a scoped artifact operation, then
    /// optimize the operational documents (plan/details/context) via a second scoped artifact operation (CLI-only;
    /// the backend transfer does not run the optimization — see technical-debt.md). The fresh process is NOT
    /// reseeded here — RunAsync reopens it and its next proposal primes it with the just-rewritten context
    /// inline (no legacy StartDecisionSession* turn), so this leaves the process closed.
    /// </summary>
    private async Task TransferAsync(CancellationToken cancellationToken)
    {
        _console.Phase("Decision: Transfer/ProduceOperationalDelta");
        var deltaRenderer = new ConsoleTurnRenderer(_console);
        AgentTurnResult delta = await session!.RunTurnAsync(
            ProduceOperationalDelta.Text, deltaRenderer.Stream, cancellationToken);
        if (delta.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException(WithDiagnostics(
                $"Operational-delta turn ended in state {delta.State}.", delta.Diagnostics));
        }

        deltaRenderer.EchoIfSilent(delta.Output);

        await _artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalDelta, delta.Output);

        // Close the old process (resets seeded + token pressure).
        await CloseAsync();

        _console.Phase("Decision: Transfer/UpdateOperationalContext");
        AgentTurnResult update = await EvolveOperationalContextAsync(delta.Output, cancellationToken);

        _console.Phase("Decision: Transfer/OptimizeOperationalDocuments");
        AgentTurnResult optimize = await OptimizeOperationalDocumentsAsync(cancellationToken);

        // Archive the consumed operational delta now that operational_context.md is successfully updated: rotate
        // .agents/operational_delta.md into a numbered .agents/deltas/ copy and remove the live file. Hard step —
        // a missing delta or a failed rotation fails the transfer (the old process is already closed above; no
        // session is open to tear down here).
        _console.Phase("Decision: Transfer/ArchiveOperationalDelta");
        string? archived;
        try
        {
            archived = await _artifacts.RotateOperationalDeltaAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new LoopStepException("Transfer failed to archive operational_delta.md.", exception);
        }

        if (archived is null)
        {
            throw new LoopStepException("Transfer produced no operational_delta.md to archive.");
        }

        // The fresh process is left CLOSED (seeded stays false): RunAsync reopens it and its next proposal primes
        // it with the just-rewritten operational context inline (see BuildProposalPromptAsync). No reseed turn.

        // Cost-aware accounting: record the MEASURED transfer cost (delta + evolution + optimization) so the
        // router's transfer-cost estimate (C) self-calibrates off reality. CloseAsync above reset the per-process
        // reuse accounting; the fresh process's context-priming cost is captured by the next proposal's
        // RecordProposalCost. transferCost persists across recycles.
        RecordTransferCost(
            (_costModel ?? new EffectiveTokenCostModel()).Measure(delta.Usage) + (_costModel ?? new EffectiveTokenCostModel()).Measure(update.Usage) + (_costModel ?? new EffectiveTokenCostModel()).Measure(optimize.Usage));
    }

    // Evolves the operational context through a fresh app-server session scoped to the context and delta artifacts.
    // Direct repository writes are wrapped in a rollback transaction so a failed turn/gate preserves inputs.
    private async Task<AgentTurnResult> EvolveOperationalContextAsync(
        string deltaOutput, CancellationToken cancellationToken)
    {
        await _artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalDelta, deltaOutput);

        var operation = new DecisionArtifactOperation(
            Label: "operational-context-evolution",
            Prompt: UpdateOperationalContext.Text,
            Profile: new OperationPermissionProfile(
                "operational-context-evolution",
                _repository.Path,
                [OrchestrationArtifactPaths.OperationalContext, OrchestrationArtifactPaths.OperationalDelta],
                [],
                [OrchestrationArtifactPaths.OperationalContext],
                []),
            RequiredOutputs: [OrchestrationArtifactPaths.OperationalContext],
            ChangedGuard: OrchestrationArtifactPaths.OperationalContext);

        return await RunArtifactOperationAsync(
            operation,
            "Transfer left no operational_context.md to seed the next decision session from.",
            "evolution left operational_context.md unchanged — the operational delta was not applied",
            cancellationToken);
    }

    // The operational documents the post-evolution optimization operation is scoped to.
    private static readonly string[] OptimizationDocuments =
    [
        OrchestrationArtifactPaths.Plan,
        OrchestrationArtifactPaths.Details,
        OrchestrationArtifactPaths.OperationalContext,
    ];

    // Immediately after the context evolution, optimize the operational documents in a second scoped app-server
    // session. Optional documents may be touched only when they existed before this operation.
    private async Task<AgentTurnResult> OptimizeOperationalDocumentsAsync(CancellationToken cancellationToken)
    {
        var existingDocuments = new List<string>();
        foreach (string document in OptimizationDocuments)
        {
            if (await _artifacts.ExistsAsync(document))
            {
                existingDocuments.Add(document);
            }
        }

        var operation = new DecisionArtifactOperation(
            Label: "operational-documents-optimization",
            Prompt: OptimizeOperationalDocuments.Text,
            Profile: new OperationPermissionProfile(
                "operational-documents-optimization",
                _repository.Path,
                existingDocuments,
                [],
                existingDocuments,
                []),
            RequiredOutputs: [OrchestrationArtifactPaths.OperationalContext],
            ChangedGuard: null);

        AgentTurnResult optimize = await RunArtifactOperationAsync(
            operation,
            "Optimization left no operational_context.md to seed the next decision session from.",
            unchangedGuardFailure: null,
            cancellationToken);

        string optimizedContext = await _artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;
        RecordOperationalContextHealth(optimizedContext.Length);
        return optimize;
    }

    private async Task<AgentTurnResult> RunArtifactOperationAsync(
        DecisionArtifactOperation operation,
        string missingRequiredMessage,
        string? unchangedGuardFailure,
        CancellationToken cancellationToken)
    {
        string? changedGuardSnapshot = null;
        foreach (string read in operation.Profile.AllowedReads)
        {
            string? content = await _artifacts.ReadAsync(read);
            if (content is null)
            {
                throw new LoopStepException($"{operation.Label}: required input {read} was not found.");
            }

            if (operation.ChangedGuard is not null && string.Equals(read, operation.ChangedGuard, StringComparison.Ordinal))
            {
                changedGuardSnapshot = content;
            }
        }

        ArtifactMutationTransaction transaction =
            await ArtifactMutationTransaction.CaptureAsync(_artifacts.Store, _repository, operation.Profile);

        IAgentSession? scopedSession = null;
        bool keepChanges = false;
        try
        {
            var renderer = new ConsoleTurnRenderer(_console);
            scopedSession = await _runtime.OpenSessionAsync(
                AgentSpecs.ScopedArtifactOperation(
                    _repository,
                    AgentEffortLevel.High,
                    identifier: "xhigh",
                    operation.Profile),
                cancellationToken);
            AgentTurnResult result = await scopedSession.RunTurnAsync(
                operation.Prompt,
                renderer.Stream,
                cancellationToken);
            if (result.State != AgentTurnState.Completed)
            {
                throw new LoopStepException(WithDiagnostics(
                    $"{operation.Label} turn ended in state {result.State}.", result.Diagnostics));
            }

            renderer.EchoIfSilent(result.Output);

            IReadOnlyList<string> deleted = await transaction.DeletedSnapshotFilesAsync();
            if (deleted.Count > 0)
            {
                throw new LoopStepException(
                    $"{operation.Label} deleted declared artifact(s): {string.Join(", ", deleted)}.");
            }

            foreach (string requiredOutput in operation.RequiredOutputs)
            {
                if (!await _artifacts.ExistsAsync(requiredOutput))
                {
                    throw new LoopStepException(missingRequiredMessage);
                }
            }

            if (operation.ChangedGuard is { } changedGuard)
            {
                string changedContent = await _artifacts.ReadAsync(changedGuard) ?? string.Empty;
                if (string.Equals(changedContent, changedGuardSnapshot ?? string.Empty, StringComparison.Ordinal))
                {
                    throw new LoopStepException(unchangedGuardFailure ?? $"{operation.Label} left {changedGuard} unchanged.");
                }
            }

            keepChanges = true;
            return result;
        }
        catch
        {
            if (!keepChanges)
            {
                await transaction.RestoreAsync();
            }

            throw;
        }
        finally
        {
            if (scopedSession is not null)
            {
                await _runtime.CloseSessionAsync(scopedSession);
            }
        }
    }

    private sealed record DecisionArtifactOperation(
        string Label,
        string Prompt,
        OperationPermissionProfile Profile,
        IReadOnlyList<string> RequiredOutputs,
        string? ChangedGuard);

    // Size-health guard: warn on a sustained upward ratchet of the operational-context size across consecutive
    // transfers. Kept local so the CLI does not depend on the legacy orchestration host's monitor service.
    private void RecordOperationalContextHealth(int newSize)
    {
        if (previousOperationalContextSize is null)
        {
            previousOperationalContextSize = newSize;
            operationalContextGrowthStreak = 0;
            return;
        }

        operationalContextGrowthStreak = newSize > previousOperationalContextSize.Value
            ? operationalContextGrowthStreak + 1
            : 0;
        previousOperationalContextSize = newSize;
        if (operationalContextGrowthStreak >= OperationalContextGrowthStreakWarningThreshold)
        {
            _console.Warn($"Operational context has grown for {operationalContextGrowthStreak} consecutive transfers (now {newSize} chars) — check for bloat.");
        }
    }

    private async Task<ProjectContextProjectionResult> EnsureDecisionProjectionAsync(CancellationToken cancellationToken)
    {
        if (_projectionService is null)
        {
            return new ProjectContextProjectionResult(
                new ProjectionDefinition(
                    ProjectionRuntimePromptNames.DecisionSession,
                    "ProjectionForDecisionSession",
                    ProjectionArtifactPaths.ProjectionPaths[ProjectionRuntimePromptNames.DecisionSession],
                    "# Execution Agent System Prompt Projection",
                    ProjectionRuntimePromptNames.DecisionSession),
                string.Empty,
                Generated: false,
                ProjectionStaleStatus.UnknownProvenance,
                []);
        }

        try
        {
            return await _projectionService.EnsureFreshAsync(
                ProjectionRuntimePromptNames.DecisionSession,
                cancellationToken);
        }
        catch (ProjectionException ex)
        {
            throw new LoopStepException(ex.Message, ex);
        }
    }

    private async Task<ProjectionFreshness> EvaluateDecisionProjectionFreshnessAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _projectionService!.EvaluateFreshnessAsync(
                ProjectionRuntimePromptNames.DecisionSession,
                cancellationToken);
        }
        catch (ProjectionException ex)
        {
            throw new LoopStepException(ex.Message, ex);
        }
    }

    // The router's unit-blind signals (mirrors RepositoryOrchestrator.SnapshotRouterInputs). Before any cycle is
    // observed (n == 0), occupancy is 0 so only the capacity guard could fire (it won't on a fresh process).
    private RouterInputs BuildRouterInputs()
    {
        if (reuseCycles == 0)
        {
            return new RouterInputs(0, 0d, 0, 0d, transferCost);
        }

        double predictedNext = (_costModel ?? new EffectiveTokenCostModel()).EstimateNextCycle(
            new DecisionCostForecast(lastCycleCost, prevCycleCost, occupancyTokens, 0));
        return new RouterInputs(occupancyTokens, reuseCost, reuseCycles, predictedNext, transferCost);
    }

    private void RecordProposalCost(AgentTokenUsage usage)
    {
        double cost = (_costModel ?? new EffectiveTokenCostModel()).Measure(usage);
        occupancyTokens = usage.PromptTokens + usage.OutputTokens;
        reuseCost += cost;
        reuseCycles += 1;
        prevCycleCost = lastCycleCost;
        lastCycleCost = cost;
    }

    private void RecordTransferCost(double measuredCost)
    {
        transferCount += 1;
        transferCost = transferCount == 1
            ? measuredCost
            : transferCost + ((measuredCost - transferCost) / transferCount);
    }

    // A failed turn's Diagnostics (the codex process's retained stderr tail) rides along in the thrown
    // message so the actual refusal/error text reaches the operator instead of a bare turn state.
    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";

    // clearResumeState: a Transfer recycle or a failed turn ends the thread's useful life — the persisted
    // resume state must die with it (the recycled process re-persists after its first successful turn).
    // Disposal (loop exit) KEEPS the state: it is precisely the next run's resume payload, and no turn can
    // mutate the thread between the last persist and disposal.
    private async Task CloseAsync(bool clearResumeState = true)
    {
        if (session is not null)
        {
            await _runtime.CloseSessionAsync(session);
            session = null;
            seeded = false;
            // Per-process accounting resets for the fresh process; transferCost/transferCount persist.
            occupancyTokens = 0;
            reuseCost = 0d;
            reuseCycles = 0;
            lastCycleCost = 0d;
            prevCycleCost = 0d;

            if (clearResumeState)
            {
                await (_resumeStore ?? new NullDecisionSessionResumeStore()).ClearAsync(CancellationToken.None);
            }
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync(clearResumeState: false);
}
