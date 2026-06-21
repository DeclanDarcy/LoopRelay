using CommandCenter.Core.Repositories;

namespace CommandCenter.Core.Artifacts;

public interface IArtifactService
{
    Task<IReadOnlyList<Artifact>> DiscoverAsync(Repository repository);

    Task<Artifact?> GetCurrentHandoffAsync(Repository repository);

    Task<Artifact?> GetCurrentOperationalContextAsync(Repository repository);

    Task<Artifact?> GetCurrentDecisionsAsync(Repository repository);

    Task<bool> ExistsAsync(Repository repository, string relativePath);

    Task<string> LoadAsync(Repository repository, string relativePath);

    Task SaveAsync(Repository repository, string relativePath, string content);
}
