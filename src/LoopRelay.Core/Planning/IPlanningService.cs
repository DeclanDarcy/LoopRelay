using LoopRelay.Core.Repositories;

namespace LoopRelay.Core.Planning;

public sealed class Milestone
{
    public string Name { get; init; } = "";

    public string RelativePath { get; init; } = "";
}

public enum ExecutionReadiness
{
    MissingPlan,
    MissingMilestones,
    Ready
}

public interface IPlanningService
{
    Task<bool> HasPlanAsync(Repository repository);

    Task<IReadOnlyList<Milestone>> GetMilestonesAsync(Repository repository);

    Task<ExecutionReadiness> DetermineReadinessAsync(Repository repository);
}
