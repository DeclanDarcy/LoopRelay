using CommandCenter.Core.Repositories;

namespace CommandCenter.Backend.Endpoints;

internal static class RepositoryEndpointExtensions
{
    internal static async Task<Repository> GetRepositoryAsync(
        this IRepositoryService repositoryService,
        Guid repositoryId)
    {
        Repository? repository = (await repositoryService.GetAllAsync())
            .FirstOrDefault(repository => repository.Id == repositoryId);
        return repository ?? throw new KeyNotFoundException($"Repository was not found: {repositoryId}");
    }
}
