using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.DecisionSessions.Persistence;

public static class DecisionSessionArtifactPaths
{
    public const string SchemaVersion = "decision-sessions.v1";

    public static string RegistryJson() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "registry.json");

    public static string MetricsSnapshotJson() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "analysis", "metrics", "snapshot.json");

    public static string Resolve(Repository repository, string relativePath)
    {
        return ArtifactPath.ResolveRepositoryPath(repository, relativePath);
    }
}
