namespace CommandCenter.Backend.Projections;

public interface IRepositoryProjectionService
{
    Task<IReadOnlyList<RepositoryDashboardProjection>> GetDashboardAsync();

    Task<RepositoryWorkspaceProjection> GetWorkspaceAsync(Guid repositoryId);

    Task<RepositoryWorkspaceProjection> RefreshWorkspaceAsync(Guid repositoryId);
}
