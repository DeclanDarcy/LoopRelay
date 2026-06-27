namespace CommandCenter.Orchestration.Models;

/// <summary>
/// The transient, in-memory marker stored under the reserved <c>{repositoryId}:Plan</c> cache key
/// while a run is active. It is deliberately a projection of run state, NOT durable authority —
/// a process restart drops it, and the durable projection is rebuilt from repository artifacts.
/// </summary>
public sealed record ActiveRunSnapshot(int Iteration, bool PlanCached);
