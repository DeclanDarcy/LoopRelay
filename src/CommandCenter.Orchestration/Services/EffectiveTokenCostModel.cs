using CommandCenter.Agents.Models;
using CommandCenter.Orchestration.Abstractions;
using CommandCenter.Orchestration.Models;

namespace CommandCenter.Orchestration.Services;

/// <summary>
/// Default <see cref="IDecisionCostModel"/>: cost = cache-adjusted effective tokens. Cached input is the cheap
/// part (it bills at <see cref="cacheCostFactor"/>, default 0.10), so carrying a warm, mostly-cached context is
/// far cheaper than its raw token count suggests — which is exactly why the router must weight it. In the common
/// case cost rises with run age (the warm context keeps growing), which is the router's optimality precondition —
/// but that is a WORKLOAD assumption, not a property this per-turn function enforces (a smaller later turn yields a
/// smaller cost; see the floor in <see cref="EstimateNextCycle"/>).
/// <para>
/// The seam lets dollars/latency models replace this later without touching the router; this is the one
/// concrete model shipped (no speculative alternatives — YAGNI).
/// </para>
/// </summary>
public sealed class EffectiveTokenCostModel : IDecisionCostModel
{
    private readonly double cacheCostFactor;

    public EffectiveTokenCostModel(double cacheCostFactor = 0.10)
    {
        this.cacheCostFactor = cacheCostFactor;
    }

    public double Measure(AgentTokenUsage turn)
    {
        // Clamp every field to be non-negative FIRST, so a malformed report (e.g. a negative token count) degrades
        // to a sane cost instead of throwing — Math.Clamp(cached, 0, prompt) would itself throw if prompt < 0.
        int prompt = Math.Max(0, turn.PromptTokens);
        int output = Math.Max(0, turn.OutputTokens);
        int cached = Math.Clamp(turn.CachedInputTokens, 0, prompt); // cached is a subset of input
        int fresh = prompt - cached;
        return fresh + (cached * cacheCostFactor) + output;
    }

    public double EstimateNextCycle(DecisionCostForecast forecast)
    {
        if (forecast.LastCycleCost <= 0d)
        {
            return forecast.LastCycleCost;
        }

        // Velocity extrapolation, floored at the last observed cost: the reuse curve only rises, so never predict
        // a cheaper next cycle (that would delay a transfer that is already due).
        double velocity = forecast.LastCycleCost - forecast.PreviousCycleCost;
        return Math.Max(forecast.LastCycleCost, forecast.LastCycleCost + velocity);
    }
}
