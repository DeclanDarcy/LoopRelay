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
/// the agent's proposal verbatim). Owns ONE warm read-only process reused across iterations; seeds once
/// with StartDecisionSession(operationalContext); proposes with GetNextDecisions(latestHandoff); persists
/// the proposal to decisions.{N:0000}.md AND canonical decisions.md; verifies decisions.md exists.
/// </summary>
internal sealed class DecisionSession(
    IAgentRuntime runtime,
    IDecisionSessionRouter router,
    LoopArtifacts artifacts,
    ILoopConsole console,
    Repository repository,
    IDecisionCostModel? costModel = null) : IAsyncDisposable
{
    private readonly IDecisionCostModel costModel = costModel ?? new EffectiveTokenCostModel();
    private IAgentSession? session;
    private bool seeded;

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

        await EnsureSeededAsync(cancellationToken);

        (string? handoff, _) = await artifacts.ReadLatestHandoffAsync();
        if (handoff is null)
        {
            throw new LoopStepException("No handoff available for the decision session.");
        }

        AgentTurnResult proposed = await session!.RunTurnAsync(
            GetNextDecisions.Render(handoff), StreamToConsole, cancellationToken);

        if (proposed.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Decision turn ended in state {proposed.State}.");
        }

        RecordProposalCost(proposed.Usage);
        console.Message(proposed.Output);

        // Auto-submit: the CLI is fully automated, so the agent's proposal is persisted verbatim.
        await artifacts.PersistDecisionsAsync(proposed.Output);

        if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions))
        {
            throw new LoopStepException(".agents/decisions/decisions.md was not written.");
        }

        console.Info("New decisions.md verified.");
    }

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        session ??= await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);
        if (seeded)
        {
            return;
        }

        await artifacts.EnsureOperationalContextAsync();
        string operationalContext = await artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(operationalContext))
        {
            console.Warn("Operational context is empty — seeding the decision session with no context.");
        }

        AgentTurnResult seed = await session.RunTurnAsync(
            StartDecisionSession.Render(operationalContext), onChunk: null, cancellationToken);

        if (seed.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Decision seed turn ended in state {seed.State}.");
        }

        seeded = true;
    }

    /// <summary>
    /// Transfer recycle, mirroring RepositoryOrchestrator.PrepareTransferAsync: extract an operational delta
    /// from the warm process, close it, rewrite operational_context.md via a one-shot operational turn, then
    /// open a FRESH decision process and seed it from the rewritten context.
    /// </summary>
    private async Task TransferAsync(CancellationToken cancellationToken)
    {
        console.Phase("Decision: Transfer/ProduceOperationalDelta");
        AgentTurnResult delta = await session!.RunTurnAsync(
            ProduceOperationalDelta.Text, StreamToConsole, cancellationToken);
        if (delta.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Operational-delta turn ended in state {delta.State}.");
        }

        await artifacts.WriteAsync(OrchestrationArtifactPaths.OperationalDelta, delta.Output);

        // Close the old process (resets seeded + token pressure).
        await CloseAsync();

        console.Phase("Decision: Transfer/UpdateOperationalContext");
        AgentTurnResult update = await runtime.RunOneShotAsync(
            AgentSpecs.Operational(repository, AgentEffortLevel.High, identifier: "xhigh"),
            UpdateOperationalContext.Text,
            StreamToConsole,
            cancellationToken);
        if (update.State != AgentTurnState.Completed)
        {
            throw new LoopStepException($"Update-operational-context turn ended in state {update.State}.");
        }

        // Open a fresh decision process and seed from the rewritten context.
        session = await runtime.OpenSessionAsync(AgentSpecs.Decision(repository), cancellationToken);
        string newContext = await artifacts.ReadAsync(OrchestrationArtifactPaths.OperationalContext) ?? string.Empty;

        console.Phase("Decision: Transfer/StartDecisionSessionFromTransfer");
        AgentTurnResult reseed = await session.RunTurnAsync(
            StartDecisionSessionFromTransfer.Render(newContext), onChunk: null, cancellationToken);
        if (reseed.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new LoopStepException($"Transfer reseed turn ended in state {reseed.State}.");
        }

        seeded = true;

        // Cost-aware accounting: record the MEASURED transfer cost (delta + evolution + reseed, same cost model)
        // so the router's transfer-cost estimate (C) self-calibrates off reality. CloseAsync above reset the
        // per-process reuse accounting for the fresh process; transferCost persists.
        RecordTransferCost(costModel.Measure(delta.Usage) + costModel.Measure(update.Usage) + costModel.Measure(reseed.Usage));
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

    private Task StreamToConsole(AgentStreamChunk chunk)
    {
        if (chunk.Stream == AgentProcessOutputStream.StandardOutput)
        {
            console.Delta(chunk.Content);
        }

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await CloseAsync();
}
