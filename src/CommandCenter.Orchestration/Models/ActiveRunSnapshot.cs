namespace CommandCenter.Orchestration.Models;

/// <summary>
/// The transient, in-memory marker stored under the reserved <c>{repositoryId}:Plan</c> cache key
/// while a run is active. It is deliberately a projection of run state, NOT durable authority —
/// a process restart drops it, and the durable projection is rebuilt from repository artifacts.
/// <see cref="Plan"/> carries the active run's plan text (m4 caches it under the same key as the run
/// crosses into execution); it is null until a plan is recorded.
/// </summary>
public sealed record ActiveRunSnapshot(int Iteration, bool PlanCached, string? Plan = null);
