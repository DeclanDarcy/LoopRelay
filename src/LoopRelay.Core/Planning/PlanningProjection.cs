namespace LoopRelay.Core.Planning;

public sealed class PlanningProjection
{
    public bool HasPlan { get; init; }

    public IReadOnlyList<Milestone> Milestones { get; init; } = [];

    public ExecutionReadiness Readiness { get; init; }
}
