using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Services.Artifacts;

namespace LoopRelay.Projections.Services.ProjectionArtifacts;

public sealed class ProjectionArtifacts(IArtifactStore _store, Repository _repository)
{
    private readonly IArtifactStore artifacts = _store is RepositoryArtifactStore
        ? _store
        : new RepositoryArtifactStore(_store, _repository);

    public Repository Repository => _repository;

    public Task<bool> ExistsAsync(string relativePath) => artifacts.ExistsAsync(relativePath);

    public Task<string?> ReadAsync(string relativePath) => artifacts.ReadAsync(relativePath);

    public Task WriteAsync(string relativePath, string content) => artifacts.WriteAsync(relativePath, content);

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        return await artifacts.ListAsync(relativeDirectory, searchPattern);
    }
}
