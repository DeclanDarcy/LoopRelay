using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;

namespace CommandCenter.DecisionSessions.Persistence;

public static class DecisionSessionArtifactPaths
{
    public const string SchemaVersion = "decision-sessions.v1";

    public static string RegistryJson() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "registry.json");

    public static string MetricsSnapshotJson() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "analysis", "metrics", "snapshot.json");

    public static string EconomicsSnapshotJson() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "analysis", "economics", "snapshot.json");

    public static string CoherenceSnapshotJson() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "analysis", "coherence", "snapshot.json");

    public static string LifecyclePolicySnapshotJson() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "lifecycle", "policy", "snapshot.json");

    public static string TransferEligibilitySnapshotJson() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "lifecycle", "eligibility", "snapshot.json");

    public static string ContinuityArtifactsDirectory() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "continuity-artifacts");

    public static string ContinuityArtifactJson(string artifactId) => ArtifactPath.CombineRelative(".agents", "decision-sessions", "continuity-artifacts", artifactId);

    public static string TransfersDirectory() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "transfers");

    public static string TransferJson(string transferId) => ArtifactPath.CombineRelative(".agents", "decision-sessions", "transfers", transferId);

    public static string RecoveryDirectory() => ArtifactPath.CombineRelative(".agents", "decision-sessions", "recovery");

    public static string RecoveryJson(string recoveryId) => ArtifactPath.CombineRelative(".agents", "decision-sessions", "recovery", recoveryId);

    public static string Resolve(Repository repository, string relativePath)
    {
        return ArtifactPath.ResolveRepositoryPath(repository, relativePath);
    }
}
