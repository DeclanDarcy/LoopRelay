namespace CommandCenter.Backend.Repositories;

public interface IRepositoryService
{
    Task<IReadOnlyList<Repository>> GetAllAsync();

    Task<Repository> RegisterAsync(string repositoryPath);

    Task RemoveAsync(Guid repositoryId);
}
