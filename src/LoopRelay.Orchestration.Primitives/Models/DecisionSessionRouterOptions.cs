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
