using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Primitives;

namespace LoopRelay.Orchestration.Services;

/// <summary>
/// Default <see cref="IDecisionSessionRouter"/>: a pure, deterministic decision over unit-blind cost signals
/// (m7). No registry, no I/O, no async — the orchestrator owns the inputs (computed via the cost model) and the
/// eligibility gate. The economic decision is the online average-cost optimum (<see cref="DecisionTransferPolicy.MarginalAverageCost"/>):
/// transfer once the predicted next cycle would raise the run's amortized average, <c>eNext ≥ (R + C) / n</c>.
/// The hard capacity guard always applies on top.
/// </summary>
public sealed class DecisionSessionRouter : IDecisionSessionRouter
{
    private readonly DecisionSessionRouterOptions options;

    public DecisionSessionRouter(DecisionSessionRouterOptions? options = null)
    {
        this.options = options ?? new DecisionSessionRouterOptions();
    }

    public DecisionRoute Evaluate(RouterInputs inputs)
    {
        // Hard capacity guard: recycle before the window overflows, independent of the economic policy.
        if (inputs.OccupancyTokens >= options.CapacityGuardTokens)
        {
            return DecisionRoute.Transfer;
        }

        bool economicTransfer = options.Policy switch
        {
            // Marginal: do the next cycle only while it is cheaper than the current amortized average (R+C)/n.
            // n == 0 (just-reseeded process) always continues — never recycle a process that has done no work
            // (also avoids divide-by-zero).
            DecisionTransferPolicy.MarginalAverageCost =>
                inputs.ReuseCycleCount >= 1
                && inputs.PredictedNextCost >= (inputs.AccumulatedReuseCost + inputs.TransferCostEstimate) / inputs.ReuseCycleCount,

            // Diagnostic: linear-growth approximation. Exact only when reuse cost grows linearly.
            DecisionTransferPolicy.LinearReuseApprox =>
                inputs.AccumulatedReuseCost >= inputs.TransferCostEstimate,

            // Safety baseline: capacity guard only, no economic transfer.
            DecisionTransferPolicy.CapacityOnly => false,

            _ => false
        };

        return economicTransfer ? DecisionRoute.Transfer : DecisionRoute.Continue;
    }
}
