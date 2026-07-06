using LoopRelay.Core.Prompts;
using LoopRelay.Core.Repositories;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Orchestration;
using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli;

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
/// codex thread id + router accounting persist to {repo}/.LoopRelay/decision-session.json after every
/// successful proposal (see OpenOrResumeSessionAsync).
/// </summary>
internal sealed class DecisionSession(
    IAgentRuntime runtime,
    IDecisionSessionRouter router,
    LoopArtifacts artifacts,
    ILoopConsole console,
    Repository repository,
    IDecisionCostModel? costModel = null,
    ISandboxWorkspaceFactory? sandboxFactory = null,
    IDecisionSessionResumeStore? resumeStore = null,
    bool resumeEnabled = true) : IAsyncDisposable
{
    private readonly IDecisionCostModel costModel = costModel ?? new EffectiveTokenCostModel();
    private readonly ISandboxWorkspaceFactory sandboxFactory = sandboxFactory ?? new TempSandboxWorkspaceFactory();
    private readonly IDecisionSessionResumeStore resumeStore = resumeStore ?? new NullDecisionSessionResumeStore();
    private IAgentSession? session;
    private bool seeded;
    private bool resumeAttempted;

    // Operational-context size-health state (Stage 2, mirrors RepositoryOrchestrator). Single-threaded, no lock.
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
        DecisionRoute route = router.Evaluate(BuildRouterInputs());
        // Eligibility downgrade: a Transfer needs a primed warm process to extract a delta from.
        if (route == DecisionRoute.Transfer && !seeded)
        {
            route = DecisionRoute.Continue;
        }

        console.Phase($"Decision (route={route})");

        if (route == DecisionRoute.Transfer)
        {
            await TransferAsync(cancellationToken);
        }

        session ??= await OpenOrResumeSessionAsync(cancellationToken);

        (string? handoff, _) = await artifacts.ReadLatestHandoffAsync();
        string proposalPrompt = await BuildProposalPromptAsync(handoff);

        // Own phase header so post-transfer proposal output no longer prints under the last
        // "Decision: Transfer/…" header.
        console.Phase("Decision: Propose");
        var proposalRenderer = new ConsoleTurnRenderer(console);
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
        await artifacts.PersistDecisionsAsync(proposed.Output);

        if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions))
        {
            throw new LoopStepException(".agents/decisions/decisions.md was not written.");
        }

        console.Info("New decisions.md verified.");
    }

    // decisions.md IS the execution agent's system prompt. The first pass (no prior handoff) authors the FIRST
    // agent's system prompt; any pass with a handoff (including the first proposal on a post-Transfer process)
    // authors the NEXT agent's, folding in the previous session's handoff. A FRESH process is also primed with
    // the operational context in this same turn (there is no separate seed) — a WARM process already carries it
    // from its first proposal, so its later proposals send only the handoff delta.
    private async Task<string> BuildProposalPromptAsync(string? handoff)
    {
        string baseline = handoff is null
            ? GenerateSystemPromptForFirstExecutionAgent.Text
            : GenerateSystemPromptForNextExecutionAgent.Render(handoff);

        if (seeded)
        {
            return baseline; // warm process: the operational context is already in this process's history
        }

        await artifacts.EnsureOperationalContextAsync();
        string operationalContext = await artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(operationalContext))
        {
            console.Warn("Operational context is empty — the decision agent has no context to work from.");
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

        DecisionSessionResumeState? state = firstOpen && resumeEnabled
            ? await resumeStore.ReadAsync(cancellationToken)
            : null;
        if (state is null)
        {
            return await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);
        }

        try
        {
            IAgentSession resumed = await runtime.OpenSessionAsync(
                AgentSpecs.Decision(repository, state.ThreadId), cancellationToken);

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
            console.Info($"Resumed decision session (thread {state.ThreadId}).");
            return resumed;
        }
        catch (AgentSessionResumeException ex)
        {
            console.Warn($"Could not resume decision session (thread {state.ThreadId}): {ex.Message} Starting fresh.");
            await resumeStore.ClearAsync(cancellationToken);
            return await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);
        }
    }

    /// <summary>
    /// The state is only ever written after a SUCCESSFUL proposal turn, so its existence implies the thread is
    /// primed (no seeded field in the schema). One small file write per decision step; the store is fail-open.
    /// </summary>
    private async Task PersistResumeStateAsync(CancellationToken cancellationToken)
    {
        if (session?.ThreadId is not { Length: > 0 } threadId)
        {
            return; // no codex thread id (legacy/one-shot shapes) — nothing a later run could resume
        }

        await resumeStore.WriteAsync(new DecisionSessionResumeState(
            threadId, occupancyTokens, reuseCost, reuseCycles, lastCycleCost, prevCycleCost,
            transferCost, transferCount, previousOperationalContextSize, operationalContextGrowthStreak),
            cancellationToken);
    }

    /// <summary>
    /// Transfer recycle, mirroring RepositoryOrchestrator.PrepareTransferAsync: extract an operational delta
    /// from the warm process, close it, rewrite operational_context.md via a one-shot operational turn, then
    /// optimize the operational documents (plan/details/context) via a second sandboxed one-shot (CLI-only;
    /// the backend transfer does not run the optimization — see technical-debt.md). The fresh process is NOT
    /// reseeded here — RunAsync reopens it and its next proposal primes it with the just-rewritten context
    /// inline (no legacy StartDecisionSession* turn), so this leaves the process closed.
    /// </summary>
    private async Task TransferAsync(CancellationToken cancellationToken)
    {
        console.Phase("Decision: Transfer/ProduceOperationalDelta");
        var deltaRenderer = new ConsoleTurnRenderer(console);
        AgentTurnResult delta = await session!.RunTurnAsync(
            ProduceOperationalDelta.Text, deltaRenderer.Stream, cancellationToken);
        if (delta.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException(WithDiagnostics(
                $"Operational-delta turn ended in state {delta.State}.", delta.Diagnostics));
        }

        deltaRenderer.EchoIfSilent(delta.Output);

        await artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalDelta, delta.Output);

        // Close the old process (resets seeded + token pressure).
        await CloseAsync();

        console.Phase("Decision: Transfer/UpdateOperationalContext");
        AgentTurnResult update = await EvolveOperationalContextAsync(delta.Output, cancellationToken);

        console.Phase("Decision: Transfer/OptimizeOperationalDocuments");
        AgentTurnResult optimize = await OptimizeOperationalDocumentsAsync(cancellationToken);

        // Archive the consumed operational delta now that operational_context.md is successfully updated: rotate
        // .agents/operational_delta.md into a numbered .agents/deltas/ copy and remove the live file. Hard step —
        // a missing delta or a failed rotation fails the transfer (the old process is already closed above; no
        // session is open to tear down here).
        console.Phase("Decision: Transfer/ArchiveOperationalDelta");
        string? archived;
        try
        {
            archived = await artifacts.RotateOperationalDeltaAsync();
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
            costModel.Measure(delta.Usage) + costModel.Measure(update.Usage) + costModel.Measure(optimize.Usage));
    }

    // One-shot sandboxes seed their inputs FLAT at the workspace root (bare filenames, not an .agents/ mirror):
    // the workspace is purpose-built for the single turn, and a dot-hidden .agents/ directory hides the seeds
    // from default listings (rg --files, Get-ChildItem without -Force) — observed to cost a live evolution agent
    // several exploration turns before it found its inputs. The one-shot prompts name the bare filenames.
    private static string SandboxSeedName(string artifactPath) => Path.GetFileName(artifactPath);

    // Stage 2 (mirrors RepositoryOrchestrator.EvolveOperationalContextAsync): evolve the operational context in an
    // ISOLATED sandbox workspace seeded with ONLY operational_context.md + operational_delta.md, so codex --cd
    // confines it there and it no longer re-explores the whole repo. The evolved context is copied back into the
    // repo (where the next proposal reads it to prime the fresh process). Returns the update turn for cost accounting.
    private async Task<AgentTurnResult> EvolveOperationalContextAsync(
        string deltaOutput, CancellationToken cancellationToken)
    {
        await using ISandboxWorkspace sandbox =
            await sandboxFactory.CreateAsync("operational-context-evolution", cancellationToken);
        string sandboxContextPath = sandbox.Resolve(SandboxSeedName(OrchestrationArtifactPaths.OperationalContext));
        string sandboxDeltaPath = sandbox.Resolve(SandboxSeedName(OrchestrationArtifactPaths.OperationalDelta));

        // Seed the workspace with EXACTLY the two evolution inputs.
        string currentContext = await artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;
        await artifacts.WriteAbsoluteAsync(sandboxContextPath, currentContext);
        await artifacts.WriteAbsoluteAsync(sandboxDeltaPath, deltaOutput);

        var updateRenderer = new ConsoleTurnRenderer(console);
        AgentTurnResult update = await runtime.RunOneShotAsync(
            AgentSpecs.Operational(repository, AgentEffortLevel.High, identifier: "xhigh", workingDirectory: sandbox.RootPath),
            UpdateOperationalContext.Text,
            updateRenderer.Stream,
            cancellationToken);
        if (update.State != AgentTurnState.Completed)
        {
            throw new LoopStepException(WithDiagnostics(
                $"Update-operational-context turn ended in state {update.State}.", update.Diagnostics));
        }

        updateRenderer.EchoIfSilent(update.Output);

        if (!await artifacts.ExistsAbsoluteAsync(sandboxContextPath))
        {
            throw new LoopStepException("Transfer left no operational_context.md to seed the next decision session from.");
        }

        string evolved = await artifacts.ReadAbsoluteAsync(sandboxContextPath) ?? string.Empty;

        // An existence check alone is self-satisfied — the CLI seeded that very file above. A turn that
        // "completed" without touching the context (e.g. the agent never ran the rewrite) means the
        // operational delta was NOT applied; failing here, BEFORE the copy-back (and thus before
        // TransferAsync archives the delta), keeps the live operational_delta.md around for a retry
        // instead of consuming it unapplied. (No such guard on the optimize step — a no-op
        // optimization is legitimate.)
        if (string.Equals(evolved, currentContext, StringComparison.Ordinal))
        {
            throw new LoopStepException(
                "evolution left operational_context.md unchanged — the operational delta was not applied");
        }

        await artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalContext, evolved); // copy the evolved context back into the repo
        // Size-health is recorded by the optimization pass that follows — one measurement per transfer, on the
        // final revision the next proposal actually reads.
        return update;
    }

    // The operational documents the post-evolution optimization one-shot is scoped to — its sandbox is seeded
    // with EXACTLY these (each existence-guarded) and only these are copied back.
    private static readonly string[] OptimizationDocuments =
    [
        OrchestrationArtifactPaths.Plan,
        OrchestrationArtifactPaths.Details,
        OrchestrationArtifactPaths.OperationalContext,
    ];

    // Immediately after the context evolution, optimize the operational documents in a SECOND isolated sandbox
    // with the same posture (codex --cd confined, workspace-write): seeded with plan.md + details.md + the
    // just-evolved operational_context.md, optimized in place, then copied back into the repo. details.md (and a
    // missing plan.md) are seeded and copied back only when present — a file the agent deleted in its sandbox is
    // left untouched in the repo, never deleted. operational_context.md must survive the turn (the next proposal
    // seeds the fresh process from it), so its absence fails the transfer, mirroring the evolution's hard check.
    private async Task<AgentTurnResult> OptimizeOperationalDocumentsAsync(CancellationToken cancellationToken)
    {
        await using ISandboxWorkspace sandbox =
            await sandboxFactory.CreateAsync("operational-documents-optimization", cancellationToken);

        foreach (string document in OptimizationDocuments)
        {
            string? content = await artifacts.ReadAsync(document);
            if (content is not null)
            {
                await artifacts.WriteAbsoluteAsync(sandbox.Resolve(SandboxSeedName(document)), content);
            }
        }

        var optimizeRenderer = new ConsoleTurnRenderer(console);
        AgentTurnResult optimize = await runtime.RunOneShotAsync(
            AgentSpecs.Operational(repository, AgentEffortLevel.High, identifier: "xhigh", workingDirectory: sandbox.RootPath),
            OptimizeOperationalDocuments.Text,
            optimizeRenderer.Stream,
            cancellationToken);
        if (optimize.State != AgentTurnState.Completed)
        {
            throw new LoopStepException(WithDiagnostics(
                $"Optimize-operational-documents turn ended in state {optimize.State}.", optimize.Diagnostics));
        }

        optimizeRenderer.EchoIfSilent(optimize.Output);

        if (!await artifacts.ExistsAbsoluteAsync(sandbox.Resolve(SandboxSeedName(OrchestrationArtifactPaths.OperationalContext))))
        {
            throw new LoopStepException("Optimization left no operational_context.md to seed the next decision session from.");
        }

        foreach (string document in OptimizationDocuments)
        {
            string absolute = sandbox.Resolve(SandboxSeedName(document));
            if (!await artifacts.ExistsAbsoluteAsync(absolute))
            {
                continue;
            }

            string optimized = await artifacts.ReadAbsoluteAsync(absolute) ?? string.Empty;
            await artifacts.WriteAsync(document, optimized);
            if (document == OrchestrationArtifactPaths.OperationalContext)
            {
                RecordOperationalContextHealth(optimized.Length);
            }
        }

        return optimize;
    }

    // Size-health guard (Stage 2, mirrors RepositoryOrchestrator.RecordOperationalContextHealth): warn on a
    // sustained upward ratchet of the operational-context size across consecutive transfers.
    private void RecordOperationalContextHealth(int newSize)
    {
        OperationalContextHealth verdict = OperationalContextHealthMonitor.Classify(
            previousOperationalContextSize, newSize, operationalContextGrowthStreak);
        previousOperationalContextSize = verdict.Size;
        operationalContextGrowthStreak = verdict.GrowthStreak;
        if (verdict.Warning)
        {
            console.Warn($"Operational context has grown for {verdict.GrowthStreak} consecutive transfers (now {verdict.Size} chars) — check for bloat.");
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

        double predictedNext = costModel.EstimateNextCycle(
            new DecisionCostForecast(lastCycleCost, prevCycleCost, occupancyTokens, 0));
        return new RouterInputs(occupancyTokens, reuseCost, reuseCycles, predictedNext, transferCost);
    }

    private void RecordProposalCost(AgentTokenUsage usage)
    {
        double cost = costModel.Measure(usage);
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
            await runtime.CloseSessionAsync(session);
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
                await resumeStore.ClearAsync(CancellationToken.None);
            }
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync(clearResumeState: false);
}
