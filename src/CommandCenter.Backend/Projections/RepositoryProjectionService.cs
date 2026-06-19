using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Projections;

public sealed class RepositoryProjectionService(IRepositoryService repositoryService) : IRepositoryProjectionService
{
    public async Task<IReadOnlyList<RepositoryDashboardProjection>> GetDashboardAsync()
    {
        var repositories = await repositoryService.GetAllAsync();
        return repositories
            .Select(repository => new RepositoryDashboardProjection
            {
                Repository = repository,
                Availability = DetermineAvailability(repository),
                Readiness = ExecutionReadiness.MissingPlan,
                MilestoneCount = 0,
                HasCurrentHandoff = false,
                HasCurrentDecisions = false
            })
            .ToArray();
    }

    public Task<RepositoryWorkspaceProjection> GetWorkspaceAsync(Guid repositoryId)
    {
        throw new NotImplementedException("Workspace projections are implemented in later milestones.");
    }

    public Task<RepositoryWorkspaceProjection> RefreshWorkspaceAsync(Guid repositoryId)
    {
        throw new NotImplementedException("Workspace refresh is implemented in later milestones.");
    }

    private static RepositoryAvailability DetermineAvailability(Repository repository)
    {
        if (!Directory.Exists(repository.Path))
        {
            return RepositoryAvailability.Missing;
        }

        try
        {
            _ = Directory.EnumerateFileSystemEntries(repository.Path).FirstOrDefault();
            var gitPath = Path.Combine(repository.Path, ".git");
            return Directory.Exists(gitPath) || File.Exists(gitPath)
                ? RepositoryAvailability.Available
                : RepositoryAvailability.Missing;
        }
        catch (UnauthorizedAccessException)
        {
            return RepositoryAvailability.AccessDenied;
        }
        catch (IOException)
        {
            return RepositoryAvailability.AccessDenied;
        }
    }
}
