using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Planning;

public sealed class PlanningService : IPlanningService
{
    public Task<bool> HasPlanAsync(Repository repository)
    {
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<Milestone>> GetMilestonesAsync(Repository repository)
    {
        return Task.FromResult<IReadOnlyList<Milestone>>(Array.Empty<Milestone>());
    }

    public Task<ExecutionReadiness> DetermineReadinessAsync(Repository repository)
    {
        return Task.FromResult(ExecutionReadiness.MissingPlan);
    }
}
