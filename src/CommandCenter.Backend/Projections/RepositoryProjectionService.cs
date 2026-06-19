namespace CommandCenter.Backend.Projections;

public sealed class RepositoryProjectionService : IRepositoryProjectionService
{
    public Task<IReadOnlyList<RepositoryDashboardProjection>> GetDashboardAsync()
    {
        return Task.FromResult<IReadOnlyList<RepositoryDashboardProjection>>(Array.Empty<RepositoryDashboardProjection>());
    }

    public Task<RepositoryWorkspaceProjection> GetWorkspaceAsync(Guid repositoryId)
    {
        throw new NotImplementedException("Workspace projections are implemented in later milestones.");
    }

    public Task<RepositoryWorkspaceProjection> RefreshWorkspaceAsync(Guid repositoryId)
    {
        throw new NotImplementedException("Workspace refresh is implemented in later milestones.");
    }
}
