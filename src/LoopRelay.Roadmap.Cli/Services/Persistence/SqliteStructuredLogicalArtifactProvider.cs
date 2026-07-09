using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal sealed class SqliteStructuredLogicalArtifactProvider(Repository repository) : ILogicalArtifactProvider
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions JournalJsonOptions = new(JsonSerializerDefaults.Web);

    public bool CanResolve(string relativePath)
    {
        string normalized = Normalize(relativePath);
        return ExactDomain(normalized) is not null ||
            normalized.StartsWith(RoadmapArtifactPaths.SplitFamiliesDirectory + "/", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<LogicalArtifactResolutionResult> ResolveAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string normalized = Normalize(relativePath);
        LogicalArtifactDescriptor descriptor = DescriptorFor(normalized);
        WorkspaceFilesystemSnapshot snapshot = await new WorkspaceSqliteStore().ReadSnapshotAsync(repository, cancellationToken);
        string? content = ContentFor(snapshot, normalized);
        if (content is null)
        {
            return LogicalArtifactResolutionResult.Unresolved(
                descriptor,
                LogicalArtifactResolutionStatus.MissingMigratedRecord,
                $"SQLite-backed migrated artifact is missing: {normalized}");
        }

        return LogicalArtifactResolutionResult.Resolved(descriptor, content);
    }

    private static string? ContentFor(WorkspaceFilesystemSnapshot snapshot, string normalized)
    {
        if (string.Equals(normalized, RoadmapArtifactPaths.DecisionLedgerJson, StringComparison.OrdinalIgnoreCase))
        {
            return SerializeStructured(snapshot.DecisionLedger);
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.StateJson, StringComparison.OrdinalIgnoreCase))
        {
            return snapshot.RoadmapState is null ? null : SerializeStructured(snapshot.RoadmapState);
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.LifecycleJson, StringComparison.OrdinalIgnoreCase))
        {
            return SerializeStructured(snapshot.ArtifactLifecycle);
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.ExecutionPreparationManifest, StringComparison.OrdinalIgnoreCase))
        {
            return SerializeManifest(snapshot.ExecutionPreparationManifest);
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.SelectionProvenanceManifest, StringComparison.OrdinalIgnoreCase))
        {
            return SerializeManifest(snapshot.SelectionProvenanceManifest);
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.ProjectionsManifestJson, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, RoadmapArtifactPaths.ProjectionsManifest, StringComparison.OrdinalIgnoreCase))
        {
            return SerializeStructured(snapshot.ProjectionManifest);
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.TransitionJournal, StringComparison.OrdinalIgnoreCase))
        {
            return snapshot.TransitionJournal.Count == 0
                ? null
                : string.Join(
                    Environment.NewLine,
                    snapshot.TransitionJournal.Select(record => JsonSerializer.Serialize(record, JournalJsonOptions))) +
                    Environment.NewLine;
        }

        if (normalized.StartsWith(RoadmapArtifactPaths.SplitFamiliesDirectory + "/", StringComparison.OrdinalIgnoreCase))
        {
            SplitFamilyFilesystemSnapshot? split = snapshot.SplitFamilies.FirstOrDefault(item =>
                string.Equals(item.RelativePath, normalized, StringComparison.OrdinalIgnoreCase));
            return split is null ? null : SerializeStructured(split.Document);
        }

        return null;
    }

    private static LogicalArtifactDescriptor DescriptorFor(string normalized)
    {
        LogicalArtifactDomain domain = ExactDomain(normalized) ??
            (normalized.StartsWith(RoadmapArtifactPaths.SplitFamiliesDirectory + "/", StringComparison.OrdinalIgnoreCase)
                ? LogicalArtifactDomain.SplitLineage
                : LogicalArtifactDomain.Unknown);
        return new LogicalArtifactDescriptor(
            normalized,
            domain,
            LogicalArtifactStorageKind.SqliteCanonicalRecord,
            domain == LogicalArtifactDomain.SplitLineage
                ? $"split-family:{normalized}"
                : normalized);
    }

    private static LogicalArtifactDomain? ExactDomain(string normalized)
    {
        if (string.Equals(normalized, RoadmapArtifactPaths.DecisionLedgerJson, StringComparison.OrdinalIgnoreCase))
        {
            return LogicalArtifactDomain.DecisionLedger;
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.StateJson, StringComparison.OrdinalIgnoreCase))
        {
            return LogicalArtifactDomain.RoadmapState;
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.LifecycleJson, StringComparison.OrdinalIgnoreCase))
        {
            return LogicalArtifactDomain.ArtifactLifecycle;
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.ExecutionPreparationManifest, StringComparison.OrdinalIgnoreCase))
        {
            return LogicalArtifactDomain.ExecutionPreparationManifest;
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.SelectionProvenanceManifest, StringComparison.OrdinalIgnoreCase))
        {
            return LogicalArtifactDomain.SelectionProvenanceManifest;
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.ProjectionsManifestJson, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, RoadmapArtifactPaths.ProjectionsManifest, StringComparison.OrdinalIgnoreCase))
        {
            return LogicalArtifactDomain.ProjectionManifest;
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.TransitionJournal, StringComparison.OrdinalIgnoreCase))
        {
            return LogicalArtifactDomain.TransitionJournal;
        }

        return null;
    }

    private static string SerializeStructured<T>(T document) =>
        JsonSerializer.Serialize(document, RoadmapJson.Options) + Environment.NewLine;

    private static string SerializeManifest<T>(T document) =>
        JsonSerializer.Serialize(document, ManifestJsonOptions) + Environment.NewLine;

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim().TrimStart('/');
}
