using LoopRelay.Core.Repositories;
using LoopRelay.Persistence.Sqlite.Abstractions;

namespace LoopRelay.Persistence.Sqlite;

/// <summary>
/// Default <see cref="IRepositoryLocator"/> backed by the registry <see cref="IRepositoryService"/>.
/// </summary>
public sealed class RepositoryServiceLocator : IRepositoryLocator
{
    private readonly IRepositoryService repositories;

    public RepositoryServiceLocator(IRepositoryService repositories)
    {
        this.repositories = repositories;
    }

    public async Task<Repository?> FindAsync(Guid repositoryId, CancellationToken ct)
    {
        IReadOnlyList<Repository> all = await repositories.GetAllAsync().ConfigureAwait(false);
        return all.FirstOrDefault(repository => repository.Id == repositoryId);
    }
}
