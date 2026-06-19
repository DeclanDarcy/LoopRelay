using CommandCenter.Backend.Configuration;

namespace CommandCenter.Backend.Repositories;

public sealed class RepositoryService(IApplicationConfigurationStore configurationStore) : IRepositoryService
{
    public async Task<IReadOnlyList<Repository>> GetAllAsync()
    {
        return (await configurationStore.LoadAsync()).Repositories;
    }

    public Task<Repository> RegisterAsync(string repositoryPath)
    {
        throw new NotImplementedException("Repository registration is implemented in M1.");
    }

    public Task RemoveAsync(Guid repositoryId)
    {
        throw new NotImplementedException("Repository removal is implemented in M1.");
    }
}
