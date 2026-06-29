using CommandCenter.Core.Repositories;

namespace CommandCenter.Persistence.Sqlite.Abstractions;

/// <summary>
/// Resolves a registered <see cref="Repository"/> from its id, so the cache layer can map the
/// <c>Guid repo</c> of the <see cref="IDerivedSnapshotCache"/> contract onto the
/// <c>&lt;repo&gt;.Path</c> the <see cref="ISqliteConnectionFactory"/> needs to open the per-repo DB.
/// A thin seam over the existing <see cref="IRepositoryService"/> registry, isolated so the cache is
/// trivially testable with an in-memory locator and never forces source-of-truth registry wiring on
/// pure-service tests.
/// </summary>
public interface IRepositoryLocator
{
    Task<Repository?> FindAsync(Guid repositoryId, CancellationToken ct);
}
