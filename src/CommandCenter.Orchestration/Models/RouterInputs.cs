namespace CommandCenter.Orchestration.Models;

/// <summary>
/// The unit-blind signals the Phase 7 router consumes to choose Continue (reuse the warm Decision process) vs
/// Transfer (recycle it). Populated by the orchestrator from the cost model; the router never sees tokens or
/// dollars, only these scalars.
/// <list type="bullet">
/// <item><see cref="OccupancyTokens"/> — current Decision-process context size (latest turn's prompt+output);
/// drives the hard capacity guard.</item>
/// <item><see cref="AccumulatedReuseCost"/> (R) — Σ cost of reuse cycles since the process last (re)started.</item>
/// <item><see cref="ReuseCycleCount"/> (n) — number of those cycles.</item>
/// <item><see cref="PredictedNextCost"/> (eNext) — predicted cost of the next reuse cycle.</item>
/// <item><see cref="TransferCostEstimate"/> (C) — measured (or seeded) cost of performing a transfer.</item>
/// </list>
/// </summary>
public sealed record RouterInputs(
    int OccupancyTokens,
    double AccumulatedReuseCost,
    int ReuseCycleCount,
    double PredictedNextCost,
    double TransferCostEstimate)
{
    public static RouterInputs Empty { get; } = new(0, 0d, 0, 0d, 0d);
}
