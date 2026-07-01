using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;
using CommandCenter.Orchestration.Services;

namespace CommandCenter.Cli;

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
/// canonical decisions.md; verifies decisions.md exists.
/// </summary>
internal sealed class DecisionSession(
    IAgentRuntime runtime,
    IDecisionSessionRouter router,
    LoopArtifacts artifacts,
    ILoopConsole console,
    Repository repository,
    IDecisionCostModel? costModel = null,
    ISandboxWorkspaceFactory? sandboxFactory = null) : IAsyncDisposable
{
    private readonly IDecisionCostModel costModel = costModel ?? new EffectiveTokenCostModel();
    private readonly ISandboxWorkspaceFactory sandboxFactory = sandboxFactory ?? new TempSandboxWorkspaceFactory();
    private IAgentSession? session;
    private bool seeded;

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

        session ??= await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);

        (string? handoff, _) = await artifacts.ReadLatestHandoffAsync();
        string proposalPrompt = await BuildProposalPromptAsync(handoff);

        var proposalRenderer = new ConsoleTurnRenderer(console);
        AgentTurnResult proposed = await session.RunTurnAsync(
            proposalPrompt, proposalRenderer.Stream, cancellationToken);

        if (proposed.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Decision turn ended in state {proposed.State}.");
        }

        // The process now holds the operational context (delivered inline in this turn), so subsequent proposals
        // on it are cheap handoff-only deltas.
        seeded = true;
        RecordProposalCost(proposed.Usage);
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
    /// Transfer recycle, mirroring RepositoryOrchestrator.PrepareTransferAsync: extract an operational delta
    /// from the warm process, close it, and rewrite operational_context.md via a one-shot operational turn. The
    /// fresh process is NOT reseeded here — RunAsync reopens it and its next proposal primes it with the just-
    /// rewritten context inline (no legacy StartDecisionSession* turn), so this leaves the process closed.
    /// </summary>
    private async Task TransferAsync(CancellationToken cancellationToken)
    {
        console.Phase("Decision: Transfer/ProduceOperationalDelta");
        AgentTurnResult delta = await session!.RunTurnAsync(
            ProduceOperationalDelta.Text, new ConsoleTurnRenderer(console).Stream, cancellationToken);
        if (delta.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Operational-delta turn ended in state {delta.State}.");
        }

        await artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalDelta, delta.Output);

        // Close the old process (resets seeded + token pressure).
        await CloseAsync();

        console.Phase("Decision: Transfer/UpdateOperationalContext");
        AgentTurnResult update = await EvolveOperationalContextAsync(delta.Output, cancellationToken);

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

        // Cost-aware accounting: record the MEASURED transfer cost (delta + evolution) so the router's transfer-cost
        // estimate (C) self-calibrates off reality. CloseAsync above reset the per-process reuse accounting; the
        // fresh process's context-priming cost is captured by the next proposal's RecordProposalCost. transferCost
        // persists across recycles.
        RecordTransferCost(costModel.Measure(delta.Usage) + costModel.Measure(update.Usage));
    }

    // Stage 2 (mirrors RepositoryOrchestrator.EvolveOperationalContextAsync): evolve the operational context in an
    // ISOLATED sandbox workspace seeded with ONLY operational_context.md + operational_delta.md, so codex --cd
    // confines it there and it no longer re-explores the whole repo. The evolved context is copied back into the
    // repo (where the next proposal reads it to prime the fresh process). Returns the update turn for cost accounting.
    private async Task<AgentTurnResult> EvolveOperationalContextAsync(
        string deltaOutput, CancellationToken cancellationToken)
    {
        await using ISandboxWorkspace sandbox =
            await sandboxFactory.CreateAsync("operational-context-evolution", cancellationToken);
        string sandboxContextPath = sandbox.Resolve(OrchestrationArtifactPaths.OperationalContext);
        string sandboxDeltaPath = sandbox.Resolve(OrchestrationArtifactPaths.OperationalDelta);

        // Seed the workspace with EXACTLY the two evolution inputs.
        string currentContext = await artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;
        await artifacts.WriteAbsoluteAsync(sandboxContextPath, currentContext);
        await artifacts.WriteAbsoluteAsync(sandboxDeltaPath, deltaOutput);

        AgentTurnResult update = await runtime.RunOneShotAsync(
            AgentSpecs.Operational(repository, AgentEffortLevel.High, identifier: "xhigh", workingDirectory: sandbox.RootPath),
            UpdateOperationalContext.Text,
            new ConsoleTurnRenderer(console).Stream,
            cancellationToken);
        if (update.State != AgentTurnState.Completed)
        {
            throw new LoopStepException($"Update-operational-context turn ended in state {update.State}.");
        }

        if (!await artifacts.ExistsAbsoluteAsync(sandboxContextPath))
        {
            throw new LoopStepException("Transfer left no operational_context.md to seed the next decision session from.");
        }

        string evolved = await artifacts.ReadAbsoluteAsync(sandboxContextPath) ?? string.Empty;
        await artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalContext, evolved); // copy the evolved context back into the repo
        RecordOperationalContextHealth(evolved.Length);
        return update;
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

    private async Task CloseAsync()
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
        }
    }

    public async ValueTask DisposeAsync() => await CloseAsync();
}
