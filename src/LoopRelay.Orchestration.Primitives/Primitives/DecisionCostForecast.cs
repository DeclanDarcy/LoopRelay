namespace LoopRelay.Orchestration.Primitives;

/// <summary>
/// Inputs a cost model uses to predict the NEXT decision cycle's cost. The MVP (effective-token) model uses
/// only <see cref="LastCycleCost"/> and <see cref="PreviousCycleCost"/> (velocity extrapolation); the extra
/// fields are carried so a richer model (occupancy/cache-aware, provider pricing, …) can be dropped in later
/// WITHOUT changing the router or the seam.
/// </summary>
public readonly record struct DecisionCostForecast(
    double LastCycleCost,
    double PreviousCycleCost,
    int CurrentOccupancyTokens = 0,
    int CachedInputTokens = 0);
