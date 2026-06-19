using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Planning;

public sealed class PlanningService(IArtifactStore artifactStore) : IPlanningService
{
    public async Task<bool> HasPlanAsync(Repository repository)
    {
        return await artifactStore.ExistsAsync(ArtifactPath.ResolveRepositoryPath(repository, ".agents/plan.md"));
    }

    public async Task<IReadOnlyList<Milestone>> GetMilestonesAsync(Repository repository)
    {
        var milestonesPath = ArtifactPath.ResolveRepositoryPath(repository, ".agents/milestones");
        var files = await artifactStore.ListAsync(milestonesPath, "*.md");

        return files
            .Select(file => new Milestone
            {
                Name = Path.GetFileName(file),
                RelativePath = ArtifactPath.ToRepositoryRelativePath(repository, file)
            })
            .OrderBy(milestone => milestone.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<ExecutionReadiness> DetermineReadinessAsync(Repository repository)
    {
        if (!await HasPlanAsync(repository))
        {
            return ExecutionReadiness.MissingPlan;
        }

        return (await GetMilestonesAsync(repository)).Count > 0
            ? ExecutionReadiness.Ready
            : ExecutionReadiness.MissingMilestones;
    }
}
