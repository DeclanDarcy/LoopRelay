using LoopRelay.Orchestration.Abstractions;
using LoopRelay.Orchestration.Models;

namespace LoopRelay.Orchestration.Services;

/// <summary>
/// Tunable knobs for <see cref="DecisionSessionRouter"/> (m7). The router transfers either on the hard CAPACITY
/// guard (occupancy ≥ <see cref="CapacityGuardFraction"/> of <see cref="ModelContextWindowTokens"/> — you must
/// recycle before the window overflows, regardless of economics) or on the economic <see cref="Policy"/>. The
/// context window is a config constant because the agent runtime does not surface it; a deployment may override
/// any knob (e.g. bind from configuration in Program.cs) to match the deployed model.
/// </summary>
public sealed record DecisionSessionRouterOptions(
    int ModelContextWindowTokens = 256_000,
    double CapacityGuardFraction = 0.90,
    DecisionTransferPolicy Policy = DecisionTransferPolicy.MarginalAverageCost)
{
    /// <summary>Occupancy at or above which the hard capacity guard forces a transfer.</summary>
    public int CapacityGuardTokens =>
        (int)Math.Round(ModelContextWindowTokens * CapacityGuardFraction, MidpointRounding.AwayFromZero);
}

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
