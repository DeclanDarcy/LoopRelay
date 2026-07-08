using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;

namespace LoopRelay.Projections.Services;

public sealed class ProjectionArtifacts(IArtifactStore store, Repository repository)
{
    public Repository Repository => repository;

    public Task<bool> ExistsAsync(string relativePath) => store.ExistsAsync(Resolve(relativePath));

    public Task<string?> ReadAsync(string relativePath) => store.ReadAsync(Resolve(relativePath));

    public Task WriteAsync(string relativePath, string content) => store.WriteAsync(Resolve(relativePath), content);

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        IReadOnlyList<string> files = await store.ListAsync(Resolve(relativeDirectory), searchPattern);
        return files.Select(path => ArtifactPath.ToRepositoryRelativePath(repository, path)).ToList();
    }

    public string Resolve(string relativePath) => ArtifactPath.ResolveRepositoryPath(repository, relativePath);
}
