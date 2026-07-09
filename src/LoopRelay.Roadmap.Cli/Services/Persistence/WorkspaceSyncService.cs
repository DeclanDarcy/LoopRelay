using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Orchestration.Services;
using LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal enum WorkspaceSyncDomain
{
    Core,
    Metadata,
    Journal,
    LoopHistories,
    ExecutionEvidence,
}

internal sealed record WorkspaceSyncOptions(
    IReadOnlySet<WorkspaceSyncDomain>? Domains = null,
    bool ForceImport = false,
    bool ForceExport = false);

internal sealed record WorkspaceSyncMarker(
    WorkspaceSyncDomain Domain,
    string CanonicalHash,
    string? ExportHash,
    long Generation,
    DateTimeOffset UpdatedAt);

internal sealed record WorkspaceSyncMarkerUpdate(
    WorkspaceSyncDomain Domain,
    string CanonicalHash,
    string ExportHash);

internal interface IWorkspaceSyncService
{
    Task<WorkspaceSqliteOperationResult> ExportAsync(
        RoadmapArtifacts artifacts,
        WorkspaceSyncOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSqliteOperationResult> ImportAsync(
        RoadmapArtifacts artifacts,
        WorkspaceSyncOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSqliteOperationResult> SyncAsync(
        RoadmapArtifacts artifacts,
        WorkspaceSyncOptions? options = null,
        CancellationToken cancellationToken = default);
}

internal sealed class WorkspaceSyncService(WorkspaceSqliteStore? store = null) : IWorkspaceSyncService
{
    private readonly WorkspaceSqliteStore _store = store ?? new WorkspaceSqliteStore();
    private readonly WorkspaceFilesystemSnapshotStore _filesystemSnapshots = new();

    public async Task<WorkspaceSqliteOperationResult> ExportAsync(
        RoadmapArtifacts artifacts,
        WorkspaceSyncOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        WorkspaceSyncOptions effectiveOptions = options ?? new WorkspaceSyncOptions();
        IReadOnlySet<WorkspaceSyncDomain> domains = WorkspaceSyncDomains.Effective(effectiveOptions.Domains);
        WorkspaceFilesystemSnapshot canonicalSnapshot = await _store.ReadSnapshotAsync(artifacts.Repository, cancellationToken);
        WorkspaceFilesystemSnapshot exportSnapshot = await _filesystemSnapshots.ImportAsync(artifacts);
        IReadOnlyDictionary<WorkspaceSyncDomain, WorkspaceSyncMarker> markers =
            await _store.ReadSyncMarkersAsync(artifacts.Repository, cancellationToken);
        if (ValidateScopedDependencies(artifacts, "export", canonicalSnapshot, domains) is { } dependencyFailure)
        {
            return dependencyFailure;
        }

        WorkspaceSyncDrift drift = WorkspaceSyncDrift.Calculate(
            domains,
            markers,
            WorkspaceSyncSnapshotHasher.HashDomains(canonicalSnapshot, domains),
            WorkspaceSyncSnapshotHasher.HashDomains(exportSnapshot, domains));

        if (!effectiveOptions.ForceExport && (drift.DivergentDomains.Count > 0 || drift.ExportChangedDomains.Count > 0))
        {
            return Blocked(
                artifacts,
                WorkspaceStorageResultCategory.Conflict,
                $"Export would overwrite changed filesystem exports: {WorkspaceSyncDomains.Describe(drift.ExportChangedDomains.Concat(drift.DivergentDomains))}.");
        }

        foreach (WorkspaceSyncDomain domain in domains)
        {
            await ExportDomainAsync(artifacts, domain, cancellationToken);
        }

        WorkspaceFilesystemSnapshot refreshedExport = await _filesystemSnapshots.ImportAsync(artifacts);
        IReadOnlyDictionary<WorkspaceSyncDomain, string> canonicalHashes =
            WorkspaceSyncSnapshotHasher.HashDomains(canonicalSnapshot, domains);
        IReadOnlyDictionary<WorkspaceSyncDomain, string> exportHashes =
            WorkspaceSyncSnapshotHasher.HashDomains(refreshedExport, domains);
        await _store.WriteSyncMarkersAsync(
            artifacts.Repository,
            MarkerUpdates(domains, canonicalHashes, exportHashes),
            cancellationToken);

        return new WorkspaceSqliteOperationResult(
            WorkspaceStorageResultCategory.Exported,
            WorkspaceDatabaseIntegrityStatus.ValidImported,
            WorkspaceDatabaseLocator.Resolve(artifacts.Repository),
            $"Workspace export completed for {WorkspaceSyncDomains.Describe(domains)}. Markers: {DescribeMarkers(canonicalHashes)}. {WorkspaceSyncReport.Describe(canonicalSnapshot, refreshedExport, domains)}.");
    }

    public async Task<WorkspaceSqliteOperationResult> ImportAsync(
        RoadmapArtifacts artifacts,
        WorkspaceSyncOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        WorkspaceSyncOptions effectiveOptions = options ?? new WorkspaceSyncOptions();
        IReadOnlySet<WorkspaceSyncDomain> domains = WorkspaceSyncDomains.Effective(effectiveOptions.Domains);
        WorkspaceFilesystemSnapshot exportSnapshot = await _filesystemSnapshots.ImportAsync(artifacts);
        WorkspaceFilesystemSnapshot? canonicalSnapshot = await TryReadCanonicalSnapshotAsync(artifacts, cancellationToken);
        IReadOnlyDictionary<WorkspaceSyncDomain, WorkspaceSyncMarker> markers =
            await _store.ReadSyncMarkersAsync(artifacts.Repository, cancellationToken);
        if (ValidateScopedDependencies(artifacts, "import", exportSnapshot, domains) is { } dependencyFailure)
        {
            return dependencyFailure;
        }

        if (canonicalSnapshot is not null)
        {
            WorkspaceSyncDrift drift = WorkspaceSyncDrift.Calculate(
                domains,
                markers,
                WorkspaceSyncSnapshotHasher.HashDomains(canonicalSnapshot, domains),
                WorkspaceSyncSnapshotHasher.HashDomains(exportSnapshot, domains));

            if (!effectiveOptions.ForceImport && drift.DivergentDomains.Count > 0)
            {
                return Blocked(
                    artifacts,
                    WorkspaceStorageResultCategory.Conflict,
                    $"Database and filesystem exports both changed: {WorkspaceSyncDomains.Describe(drift.DivergentDomains)}.");
            }

            if (!effectiveOptions.ForceImport && drift.DatabaseOnlyChangedDomains.Count > 0)
            {
                return Blocked(
                    artifacts,
                    WorkspaceStorageResultCategory.StaleExport,
                    $"Filesystem export is stale for {WorkspaceSyncDomains.Describe(drift.DatabaseOnlyChangedDomains)}.");
            }

            if (drift.UnchangedDomains.Count == domains.Count && markers.Count > 0)
            {
                return new WorkspaceSqliteOperationResult(
                    WorkspaceStorageResultCategory.Unchanged,
                    WorkspaceDatabaseIntegrityStatus.ValidImported,
                    WorkspaceDatabaseLocator.Resolve(artifacts.Repository),
                    $"Workspace import unchanged for {WorkspaceSyncDomains.Describe(domains)}.");
            }
        }

        WorkspaceSqliteOperationResult import = await _store.ImportAsync(artifacts, domains, cancellationToken);
        WorkspaceFilesystemSnapshot refreshedCanonical = await _store.ReadSnapshotAsync(artifacts.Repository, cancellationToken);
        IReadOnlyDictionary<WorkspaceSyncDomain, string> canonicalHashes =
            WorkspaceSyncSnapshotHasher.HashDomains(refreshedCanonical, domains);
        IReadOnlyDictionary<WorkspaceSyncDomain, string> exportHashes =
            WorkspaceSyncSnapshotHasher.HashDomains(exportSnapshot, domains);
        await _store.WriteSyncMarkersAsync(
            artifacts.Repository,
            MarkerUpdates(domains, canonicalHashes, exportHashes),
            cancellationToken);

        return import with
        {
            Message = $"{import.Message} Markers: {DescribeMarkers(canonicalHashes)}. {WorkspaceSyncReport.Describe(refreshedCanonical, exportSnapshot, domains)}.",
        };
    }

    public async Task<WorkspaceSqliteOperationResult> SyncAsync(
        RoadmapArtifacts artifacts,
        WorkspaceSyncOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        WorkspaceSyncOptions effectiveOptions = options ?? new WorkspaceSyncOptions();
        IReadOnlySet<WorkspaceSyncDomain> domains = WorkspaceSyncDomains.Effective(effectiveOptions.Domains);

        if (effectiveOptions.ForceImport)
        {
            await ImportAsync(artifacts, effectiveOptions with { ForceImport = true }, cancellationToken);
            return await ExportAsync(artifacts, effectiveOptions with { ForceExport = true }, cancellationToken);
        }

        if (effectiveOptions.ForceExport)
        {
            return await ExportAsync(artifacts, effectiveOptions with { ForceExport = true }, cancellationToken);
        }

        WorkspaceFilesystemSnapshot? canonicalSnapshot = await TryReadCanonicalSnapshotAsync(artifacts, cancellationToken);
        if (canonicalSnapshot is null)
        {
            await ImportAsync(artifacts, effectiveOptions, cancellationToken);
            return await ExportAsync(artifacts, effectiveOptions with { ForceExport = true }, cancellationToken);
        }

        if (ValidateScopedDependencies(artifacts, "sync", canonicalSnapshot, domains) is { } dependencyFailure)
        {
            return dependencyFailure;
        }

        WorkspaceFilesystemSnapshot exportSnapshot = await _filesystemSnapshots.ImportAsync(artifacts);
        IReadOnlyDictionary<WorkspaceSyncDomain, WorkspaceSyncMarker> markers =
            await _store.ReadSyncMarkersAsync(artifacts.Repository, cancellationToken);

        if (domains.Any(domain => !markers.ContainsKey(domain)))
        {
            await ImportAsync(artifacts, effectiveOptions, cancellationToken);
            return await ExportAsync(artifacts, effectiveOptions with { ForceExport = true }, cancellationToken);
        }

        WorkspaceSyncDrift drift = WorkspaceSyncDrift.Calculate(
            domains,
            markers,
            WorkspaceSyncSnapshotHasher.HashDomains(canonicalSnapshot, domains),
            WorkspaceSyncSnapshotHasher.HashDomains(exportSnapshot, domains));

        if (drift.DivergentDomains.Count > 0)
        {
            return Blocked(
                artifacts,
                WorkspaceStorageResultCategory.Conflict,
                $"Database and filesystem exports both changed: {WorkspaceSyncDomains.Describe(drift.DivergentDomains)}.");
        }

        if (drift.DatabaseOnlyChangedDomains.Count > 0)
        {
            return await ExportAsync(artifacts, effectiveOptions with { ForceExport = true }, cancellationToken);
        }

        if (drift.ExportOnlyChangedDomains.Count > 0)
        {
            await ImportAsync(artifacts, effectiveOptions, cancellationToken);
            return await ExportAsync(artifacts, effectiveOptions with { ForceExport = true }, cancellationToken);
        }

        return new WorkspaceSqliteOperationResult(
            WorkspaceStorageResultCategory.Unchanged,
            WorkspaceDatabaseIntegrityStatus.ValidImported,
            WorkspaceDatabaseLocator.Resolve(artifacts.Repository),
            $"Workspace sync unchanged for {WorkspaceSyncDomains.Describe(domains)}. {WorkspaceSyncReport.Empty(domains)}.");
    }

    private static WorkspaceSqliteOperationResult? ValidateScopedDependencies(
        RoadmapArtifacts artifacts,
        string operation,
        WorkspaceFilesystemSnapshot snapshot,
        IReadOnlySet<WorkspaceSyncDomain> domains)
    {
        WorkspaceSyncDependencyValidation validation = WorkspaceSyncDependencyValidator.Validate(snapshot, domains);
        if (validation.IsValid)
        {
            return null;
        }

        return Blocked(
            artifacts,
            WorkspaceStorageResultCategory.ValidationFailure,
            $"Scoped workspace {operation} would leave unresolved cross-domain references. Include dependent domains: {validation.Describe()}.");
    }

    private async Task<WorkspaceFilesystemSnapshot?> TryReadCanonicalSnapshotAsync(
        RoadmapArtifacts artifacts,
        CancellationToken cancellationToken)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(artifacts.Repository);
        if (!File.Exists(databasePath))
        {
            return null;
        }

        return await _store.ReadSnapshotAsync(artifacts.Repository, cancellationToken);
    }

    private async Task ExportDomainAsync(
        RoadmapArtifacts artifacts,
        WorkspaceSyncDomain domain,
        CancellationToken cancellationToken)
    {
        switch (domain)
        {
            case WorkspaceSyncDomain.Core:
                await _store.ExportCoreAsync(artifacts, cancellationToken);
                break;
            case WorkspaceSyncDomain.Metadata:
                await _store.ExportMetadataAsync(artifacts, cancellationToken);
                break;
            case WorkspaceSyncDomain.Journal:
                await _store.ExportJournalAsync(artifacts, cancellationToken);
                break;
            case WorkspaceSyncDomain.LoopHistories:
                await _store.ExportLoopHistoriesAsync(artifacts, cancellationToken);
                break;
            case WorkspaceSyncDomain.ExecutionEvidence:
                await _store.ExportExecutionEvidenceAsync(artifacts, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Unsupported sync domain `{domain}`.");
        }
    }

    private static WorkspaceSqliteOperationResult Blocked(
        RoadmapArtifacts artifacts,
        WorkspaceStorageResultCategory category,
        string message) =>
        new(
            category,
            category == WorkspaceStorageResultCategory.StaleExport
                ? WorkspaceDatabaseIntegrityStatus.ValidCanonical
                : WorkspaceDatabaseIntegrityStatus.IncompatiblePartialState,
            WorkspaceDatabaseLocator.Resolve(artifacts.Repository),
            message);

    private static IReadOnlyList<WorkspaceSyncMarkerUpdate> MarkerUpdates(
        IReadOnlySet<WorkspaceSyncDomain> domains,
        IReadOnlyDictionary<WorkspaceSyncDomain, string> canonicalHashes,
        IReadOnlyDictionary<WorkspaceSyncDomain, string> exportHashes) =>
        domains
            .Order()
            .Select(domain => new WorkspaceSyncMarkerUpdate(domain, canonicalHashes[domain], exportHashes[domain]))
            .ToArray();

    private static string DescribeMarkers(IReadOnlyDictionary<WorkspaceSyncDomain, string> hashes) =>
        string.Join(
            ", ",
            hashes.OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key}={pair.Value[..12]}"));
}

internal static class WorkspaceSyncDomains
{
    public static IReadOnlySet<WorkspaceSyncDomain> All { get; } = new SortedSet<WorkspaceSyncDomain>(
    [
        WorkspaceSyncDomain.Core,
        WorkspaceSyncDomain.Metadata,
        WorkspaceSyncDomain.Journal,
        WorkspaceSyncDomain.LoopHistories,
        WorkspaceSyncDomain.ExecutionEvidence,
    ]);

    public static IReadOnlySet<WorkspaceSyncDomain> Effective(IReadOnlySet<WorkspaceSyncDomain>? domains) =>
        domains is null || domains.Count == 0
            ? All
            : new SortedSet<WorkspaceSyncDomain>(domains);

    public static string Describe(IEnumerable<WorkspaceSyncDomain> domains) =>
        string.Join(", ", domains.Distinct().Order().Select(domain => domain.ToString()));

    public static bool TryParse(string value, out WorkspaceSyncDomain domain)
    {
        string normalized = value.Trim().Replace("_", "-", StringComparison.Ordinal).ToLowerInvariant();
        domain = normalized switch
        {
            "core" => WorkspaceSyncDomain.Core,
            "metadata" => WorkspaceSyncDomain.Metadata,
            "journal" => WorkspaceSyncDomain.Journal,
            "loop-history" or "loop-histories" or "histories" => WorkspaceSyncDomain.LoopHistories,
            "execution-evidence" or "evidence" => WorkspaceSyncDomain.ExecutionEvidence,
            _ => default,
        };
        return normalized is "core" or "metadata" or "journal" or "loop-history" or "loop-histories" or "histories" or "execution-evidence" or "evidence";
    }
}

internal static class WorkspaceSyncSnapshotHasher
{
    private static readonly JsonSerializerOptions HashJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyDictionary<WorkspaceSyncDomain, string> HashDomains(
        WorkspaceFilesystemSnapshot snapshot,
        IReadOnlySet<WorkspaceSyncDomain> domains) =>
        domains.ToDictionary(domain => domain, domain => HashDomain(snapshot, domain));

    private static string HashDomain(WorkspaceFilesystemSnapshot snapshot, WorkspaceSyncDomain domain)
    {
        object value = domain switch
        {
            WorkspaceSyncDomain.Core => new
            {
                snapshot.DecisionLedger,
                snapshot.RoadmapState,
                snapshot.ArtifactLifecycle,
                snapshot.SplitFamilies,
            },
            WorkspaceSyncDomain.Metadata => new
            {
                snapshot.ExecutionPreparationManifest,
                snapshot.SelectionProvenanceManifest,
                snapshot.ProjectionManifest,
            },
            WorkspaceSyncDomain.Journal => snapshot.TransitionJournal,
            WorkspaceSyncDomain.LoopHistories => snapshot.LoopHistories,
            WorkspaceSyncDomain.ExecutionEvidence => snapshot.ExecutionEvidence,
            _ => throw new InvalidOperationException($"Unsupported sync domain `{domain}`."),
        };

        return WorkspaceSqliteStore.Sha256($"{domain}:{JsonSerializer.Serialize(value, HashJsonOptions)}");
    }
}

internal sealed record WorkspaceSyncDependencyValidation(
    IReadOnlyDictionary<WorkspaceSyncDomain, IReadOnlySet<WorkspaceSyncDomain>> MissingDependencies)
{
    public bool IsValid => MissingDependencies.Count == 0;

    public string Describe() =>
        string.Join(
            "; ",
            MissingDependencies
                .OrderBy(pair => pair.Key)
                .Select(pair => $"{pair.Key} -> {WorkspaceSyncDomains.Describe(pair.Value)}"));
}

internal static class WorkspaceSyncDependencyValidator
{
    public static WorkspaceSyncDependencyValidation Validate(
        WorkspaceFilesystemSnapshot snapshot,
        IReadOnlySet<WorkspaceSyncDomain> domains)
    {
        if (domains.Count == WorkspaceSyncDomains.All.Count)
        {
            return new WorkspaceSyncDependencyValidation(
                new Dictionary<WorkspaceSyncDomain, IReadOnlySet<WorkspaceSyncDomain>>());
        }

        var missing = new SortedDictionary<WorkspaceSyncDomain, IReadOnlySet<WorkspaceSyncDomain>>();
        foreach (WorkspaceSyncDomain domain in domains.Order())
        {
            var dependencies = new SortedSet<WorkspaceSyncDomain>(ReferencedDomains(snapshot, domain)
                .Where(dependency => dependency != domain && !domains.Contains(dependency)));
            if (dependencies.Count > 0)
            {
                missing[domain] = dependencies;
            }
        }

        return new WorkspaceSyncDependencyValidation(missing);
    }

    private static IEnumerable<WorkspaceSyncDomain> ReferencedDomains(
        WorkspaceFilesystemSnapshot snapshot,
        WorkspaceSyncDomain domain)
    {
        foreach (string path in ReferencedPaths(snapshot, domain))
        {
            if (TryGetDomain(path, out WorkspaceSyncDomain dependency))
            {
                yield return dependency;
            }
        }
    }

    private static IEnumerable<string> ReferencedPaths(
        WorkspaceFilesystemSnapshot snapshot,
        WorkspaceSyncDomain domain)
    {
        switch (domain)
        {
            case WorkspaceSyncDomain.Core:
                if (snapshot.RoadmapState is { } state)
                {
                    foreach (string path in StatePaths(state))
                    {
                        yield return path;
                    }
                }

                foreach (string path in snapshot.DecisionLedger.Entries.SelectMany(entry =>
                    entry.InputArtifactPaths.Concat(entry.OutputArtifactPaths)))
                {
                    yield return path;
                }

                foreach (string path in snapshot.ArtifactLifecycle.Entries.Select(entry => entry.Path))
                {
                    yield return path;
                }

                break;
            case WorkspaceSyncDomain.Metadata:
                foreach (string path in snapshot.ExecutionPreparationManifest.MilestoneSpecs.Select(input => input.Identity))
                {
                    yield return path;
                }

                foreach (string path in DerivedArtifactPaths(snapshot.ExecutionPreparationManifest.Artifacts))
                {
                    yield return path;
                }

                foreach (string path in DerivedArtifactPaths(snapshot.SelectionProvenanceManifest.Selections))
                {
                    yield return path;
                }

                break;
            case WorkspaceSyncDomain.Journal:
                foreach (TransitionJournalRecordPath path in snapshot.TransitionJournal.SelectMany(JournalPaths))
                {
                    yield return path.Value;
                }

                break;
        }
    }

    private static IEnumerable<string> StatePaths(RoadmapStatePersistenceDocument state)
    {
        yield return state.LastTransition.Output;
        foreach (string path in state.TransitionIntent.EvidencePaths)
        {
            yield return path;
        }

        foreach (string path in state.ActiveArtifacts.Select(artifact => artifact.Path))
        {
            yield return path;
        }
    }

    private static IEnumerable<string> DerivedArtifactPaths(IEnumerable<DerivedArtifactManifestEntry> entries)
    {
        foreach (DerivedArtifactManifestEntry entry in entries)
        {
            yield return entry.ArtifactIdentity;
            foreach (DerivedArtifactCausalInput input in entry.CausalInputs)
            {
                yield return input.Identity;
            }
        }
    }

    private static IEnumerable<TransitionJournalRecordPath> JournalPaths(TransitionJournalRecord record)
    {
        foreach (string path in record.OutputPaths)
        {
            yield return new TransitionJournalRecordPath(path);
        }

        foreach (string path in record.InputArtifactHashes.Keys)
        {
            yield return new TransitionJournalRecordPath(path);
        }

        if (record.InputSnapshot is not null)
        {
            foreach (string path in record.InputSnapshot.ArtifactInputs.Select(input => input.Path))
            {
                yield return new TransitionJournalRecordPath(path);
            }
        }
    }

    private static bool TryGetDomain(string path, out WorkspaceSyncDomain domain)
    {
        string normalized = path.Replace('\\', '/').Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) ||
            string.Equals(normalized, "None", StringComparison.OrdinalIgnoreCase))
        {
            domain = default;
            return false;
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.DecisionLedgerJson, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, RoadmapArtifactPaths.StateJson, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, RoadmapArtifactPaths.LifecycleJson, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(RoadmapArtifactPaths.SplitFamiliesDirectory + "/", StringComparison.OrdinalIgnoreCase))
        {
            domain = WorkspaceSyncDomain.Core;
            return true;
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.ExecutionPreparationManifest, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, RoadmapArtifactPaths.SelectionProvenanceManifest, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, RoadmapArtifactPaths.ProjectionsManifest, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, RoadmapArtifactPaths.ProjectionsManifestJson, StringComparison.OrdinalIgnoreCase))
        {
            domain = WorkspaceSyncDomain.Metadata;
            return true;
        }

        if (string.Equals(normalized, RoadmapArtifactPaths.TransitionJournal, StringComparison.OrdinalIgnoreCase))
        {
            domain = WorkspaceSyncDomain.Journal;
            return true;
        }

        if (normalized.StartsWith(OrchestrationArtifactPaths.DecisionsDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(OrchestrationArtifactPaths.HandoffsDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(OrchestrationArtifactPaths.DeltasDirectory + "/", StringComparison.OrdinalIgnoreCase))
        {
            domain = WorkspaceSyncDomain.LoopHistories;
            return true;
        }

        if (normalized.StartsWith(RoadmapArtifactPaths.ExecutionEvidenceDirectory + "/", StringComparison.OrdinalIgnoreCase))
        {
            domain = WorkspaceSyncDomain.ExecutionEvidence;
            return true;
        }

        domain = default;
        return false;
    }

    private sealed record TransitionJournalRecordPath(string Value);
}

internal static class WorkspaceSyncReport
{
    public static string Describe(
        WorkspaceFilesystemSnapshot canonicalSnapshot,
        WorkspaceFilesystemSnapshot exportSnapshot,
        IReadOnlySet<WorkspaceSyncDomain> domains) =>
        $"Rows: {DescribeCounts(domains, domain => RowCount(canonicalSnapshot, domain))}. Files: {DescribeCounts(domains, domain => FileCount(exportSnapshot, domain))}.";

    public static string Empty(IReadOnlySet<WorkspaceSyncDomain> domains) =>
        $"Rows: {DescribeCounts(domains, _ => 0)}. Files: {DescribeCounts(domains, _ => 0)}.";

    private static string DescribeCounts(
        IReadOnlySet<WorkspaceSyncDomain> domains,
        Func<WorkspaceSyncDomain, int> count) =>
        string.Join(", ", domains.Order().Select(domain => $"{domain}={count(domain)}"));

    private static int RowCount(WorkspaceFilesystemSnapshot snapshot, WorkspaceSyncDomain domain) =>
        domain switch
        {
            WorkspaceSyncDomain.Core =>
                snapshot.DecisionLedger.Entries.Count +
                (snapshot.RoadmapState is null ? 0 : 1) +
                snapshot.ArtifactLifecycle.Entries.Count +
                snapshot.SplitFamilies.Count,
            WorkspaceSyncDomain.Metadata =>
                snapshot.ExecutionPreparationManifest.MilestoneSpecs.Count +
                snapshot.ExecutionPreparationManifest.Artifacts.Count +
                snapshot.SelectionProvenanceManifest.Selections.Count +
                snapshot.ProjectionManifest.Entries.Count,
            WorkspaceSyncDomain.Journal => snapshot.TransitionJournal.Count,
            WorkspaceSyncDomain.LoopHistories => snapshot.LoopHistories.Count,
            WorkspaceSyncDomain.ExecutionEvidence => snapshot.ExecutionEvidence.Count,
            _ => 0,
        };

    private static int FileCount(WorkspaceFilesystemSnapshot snapshot, WorkspaceSyncDomain domain) =>
        domain switch
        {
            WorkspaceSyncDomain.Core =>
                2 +
                (snapshot.RoadmapState is null ? 0 : 1) +
                snapshot.SplitFamilies.Count,
            WorkspaceSyncDomain.Metadata => 3,
            WorkspaceSyncDomain.Journal => snapshot.TransitionJournal.Count == 0 ? 0 : 1,
            WorkspaceSyncDomain.LoopHistories => snapshot.LoopHistories.Count,
            WorkspaceSyncDomain.ExecutionEvidence => snapshot.ExecutionEvidence.Count,
            _ => 0,
        };
}

internal sealed record WorkspaceSyncDrift(
    IReadOnlySet<WorkspaceSyncDomain> UnchangedDomains,
    IReadOnlySet<WorkspaceSyncDomain> DatabaseOnlyChangedDomains,
    IReadOnlySet<WorkspaceSyncDomain> ExportOnlyChangedDomains,
    IReadOnlySet<WorkspaceSyncDomain> DivergentDomains)
{
    public IReadOnlySet<WorkspaceSyncDomain> ExportChangedDomains =>
        new SortedSet<WorkspaceSyncDomain>(ExportOnlyChangedDomains.Concat(DivergentDomains));

    public static WorkspaceSyncDrift Calculate(
        IReadOnlySet<WorkspaceSyncDomain> domains,
        IReadOnlyDictionary<WorkspaceSyncDomain, WorkspaceSyncMarker> markers,
        IReadOnlyDictionary<WorkspaceSyncDomain, string> canonicalHashes,
        IReadOnlyDictionary<WorkspaceSyncDomain, string> exportHashes)
    {
        var unchanged = new SortedSet<WorkspaceSyncDomain>();
        var databaseOnly = new SortedSet<WorkspaceSyncDomain>();
        var exportOnly = new SortedSet<WorkspaceSyncDomain>();
        var divergent = new SortedSet<WorkspaceSyncDomain>();

        foreach (WorkspaceSyncDomain domain in domains)
        {
            if (!markers.TryGetValue(domain, out WorkspaceSyncMarker? marker))
            {
                unchanged.Add(domain);
                continue;
            }

            bool databaseChanged = !string.Equals(canonicalHashes[domain], marker.CanonicalHash, StringComparison.Ordinal);
            bool exportChanged = !string.Equals(exportHashes[domain], marker.ExportHash, StringComparison.Ordinal);
            if (databaseChanged && exportChanged)
            {
                divergent.Add(domain);
            }
            else if (databaseChanged)
            {
                databaseOnly.Add(domain);
            }
            else if (exportChanged)
            {
                exportOnly.Add(domain);
            }
            else
            {
                unchanged.Add(domain);
            }
        }

        return new WorkspaceSyncDrift(unchanged, databaseOnly, exportOnly, divergent);
    }
}
