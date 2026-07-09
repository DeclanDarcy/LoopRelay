using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using LoopRelay.Roadmap.Cli;
using LoopRelay.Completion.Services.ArtifactStorage;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Services;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal enum WorkspaceVerificationFindingKind
{
    StaleExport,
    MissingExport,
    UnresolvedPath,
    NondeterministicRoundTrip,
    UnrecoverableArchive,
    CorruptDomain,
    UnsupportedVersion,
    MutationRequired,
    Conflict,
    OrphanedArtifact,
    DuplicateIdentity,
    InvalidReference,
}

internal sealed record WorkspaceVerificationOptions(
    IReadOnlySet<WorkspaceSyncDomain>? Domains = null,
    bool FullRoundtrip = false);

internal sealed record WorkspaceVerificationFinding(
    WorkspaceVerificationFindingKind Kind,
    string Domain,
    string Identity,
    string Rule,
    string Severity,
    string CurrentState,
    string ExpectedState,
    string RecommendedAction);

internal sealed record WorkspaceVerificationResult(
    bool Success,
    IReadOnlyList<WorkspaceVerificationFinding> Findings)
{
    public string Summary =>
        Success
            ? "Workspace storage verification succeeded."
            : $"Workspace storage verification failed with {Findings.Count} finding(s).";
}

internal interface IWorkspaceVerificationService
{
    Task<WorkspaceVerificationResult> VerifyAsync(
        RoadmapArtifacts artifacts,
        WorkspaceVerificationOptions? options = null,
        CancellationToken cancellationToken = default);
}

internal sealed class WorkspaceVerificationService(
    WorkspaceSqliteStore? store = null,
    Func<WorkspaceFilesystemSnapshot, WorkspaceFilesystemSnapshot>? roundTripSnapshotMutator = null) : IWorkspaceVerificationService
{
    private readonly WorkspaceSqliteStore _store = store ?? new WorkspaceSqliteStore();
    private readonly WorkspaceFilesystemSnapshotStore _filesystemSnapshots = new();
    private readonly Func<WorkspaceFilesystemSnapshot, WorkspaceFilesystemSnapshot>? _roundTripSnapshotMutator =
        roundTripSnapshotMutator;

    public async Task<WorkspaceVerificationResult> VerifyAsync(
        RoadmapArtifacts artifacts,
        WorkspaceVerificationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        WorkspaceVerificationOptions effectiveOptions = options ?? new WorkspaceVerificationOptions();
        IReadOnlySet<WorkspaceSyncDomain> domains = WorkspaceSyncDomains.Effective(effectiveOptions.Domains);
        var findings = new List<WorkspaceVerificationFinding>();
        string databasePath = WorkspaceDatabaseLocator.Resolve(artifacts.Repository);
        string? beforeDatabaseHash = File.Exists(databasePath) ? FileHash(databasePath) : null;

        WorkspaceDatabaseIntegrityResult integrity = await _store.ValidateAsync(artifacts.Repository, cancellationToken);
        if (integrity.Status == WorkspaceDatabaseIntegrityStatus.Missing)
        {
            return await VerifyLegacyFilesystemWorkspaceAsync(artifacts, domains, effectiveOptions, beforeDatabaseHash, findings, cancellationToken);
        }

        if (integrity.Status == WorkspaceDatabaseIntegrityStatus.UnsupportedSchema)
        {
            findings.Add(Finding(
                WorkspaceVerificationFindingKind.UnsupportedVersion,
                "database",
                databasePath,
                "schema-version",
                integrity.Message,
                $"schema {WorkspaceSqliteStore.CurrentSchemaVersion}",
                "Run a compatible LoopRelay version or explicit migration."));
            return await FinishWithMutationGuardAsync(artifacts, domains, beforeDatabaseHash, findings);
        }

        if (integrity.Status == WorkspaceDatabaseIntegrityStatus.IncompatiblePartialState)
        {
            findings.Add(Finding(
                WorkspaceVerificationFindingKind.MutationRequired,
                "database",
                databasePath,
                "database-integrity",
                integrity.Message,
                "completed or explicitly repaired workflow transaction",
                "Retry or explicitly repair the interrupted workflow persistence phase."));
            return await FinishWithMutationGuardAsync(artifacts, domains, beforeDatabaseHash, findings);
        }

        if (integrity.Status == WorkspaceDatabaseIntegrityStatus.Corrupt)
        {
            findings.Add(Finding(
                WorkspaceVerificationFindingKind.CorruptDomain,
                "database",
                databasePath,
                "database-integrity",
                integrity.Message,
                "valid SQLite workspace database",
                "Restore from export or rerun storage-import after resolving corruption."));
            return await FinishWithMutationGuardAsync(artifacts, domains, beforeDatabaseHash, findings);
        }

        WorkspaceFilesystemSnapshot canonicalSnapshot = await _store.ReadSnapshotAsync(artifacts.Repository, cancellationToken);
        WorkspaceFilesystemSnapshot exportSnapshot;
        try
        {
            exportSnapshot = await _filesystemSnapshots.ImportAsync(artifacts);
        }
        catch (Exception exception) when (exception is RoadmapStepException or InvalidOperationException)
        {
            findings.Add(Finding(
                WorkspaceVerificationFindingKind.CorruptDomain,
                "export",
                ".agents",
                "filesystem-import",
                exception.Message,
                "importable deterministic export surface",
                "Fix malformed exports or regenerate with storage-export."));
            return await FinishWithMutationGuardAsync(artifacts, domains, beforeDatabaseHash, findings);
        }

        await AddSyncFindingsAsync(artifacts, domains, canonicalSnapshot, exportSnapshot, findings, cancellationToken);
        await AddMissingExportFindingsAsync(artifacts, domains, canonicalSnapshot, findings);
        await AddRuntimePersistenceFindingsAsync(artifacts.Repository, findings, cancellationToken);
        await AddUnresolvedPathFindingsAsync(artifacts, findings, cancellationToken);
        await AddCrossDomainIntegrityFindingsAsync(artifacts, canonicalSnapshot, findings, cancellationToken);
        await AddArchiveFindingsAsync(artifacts, findings, cancellationToken);
        await AddWorkflowRecoveryFindingsAsync(artifacts, findings, cancellationToken);

        if (effectiveOptions.FullRoundtrip)
        {
            await AddRoundTripFindingsAsync(artifacts.Repository, domains, canonicalSnapshot, findings, cancellationToken);
        }

        return await FinishWithMutationGuardAsync(artifacts, domains, beforeDatabaseHash, findings);
    }

    private async Task<WorkspaceVerificationResult> VerifyLegacyFilesystemWorkspaceAsync(
        RoadmapArtifacts artifacts,
        IReadOnlySet<WorkspaceSyncDomain> domains,
        WorkspaceVerificationOptions options,
        string? beforeDatabaseHash,
        List<WorkspaceVerificationFinding> findings,
        CancellationToken cancellationToken)
    {
        try
        {
            WorkspaceFilesystemSnapshot snapshot = await _filesystemSnapshots.ImportAsync(artifacts);
            if (options.FullRoundtrip)
            {
                await AddRoundTripFindingsAsync(artifacts.Repository, domains, snapshot, findings, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is RoadmapStepException or InvalidOperationException)
        {
            findings.Add(Finding(
                WorkspaceVerificationFindingKind.CorruptDomain,
                "legacy-export",
                ".agents",
                "filesystem-import",
                exception.Message,
                "importable legacy filesystem workspace",
                "Fix malformed legacy files before importing."));
        }

        return await FinishWithMutationGuardAsync(artifacts, domains, beforeDatabaseHash, findings);
    }

    private async Task AddSyncFindingsAsync(
        RoadmapArtifacts artifacts,
        IReadOnlySet<WorkspaceSyncDomain> domains,
        WorkspaceFilesystemSnapshot canonicalSnapshot,
        WorkspaceFilesystemSnapshot exportSnapshot,
        List<WorkspaceVerificationFinding> findings,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<WorkspaceSyncDomain, WorkspaceSyncMarker> markers =
            await _store.ReadSyncMarkersAsync(artifacts.Repository, cancellationToken);
        IReadOnlyDictionary<WorkspaceSyncDomain, string> canonicalHashes =
            WorkspaceSyncSnapshotHasher.HashDomains(canonicalSnapshot, domains);
        IReadOnlyDictionary<WorkspaceSyncDomain, string> exportHashes =
            WorkspaceSyncSnapshotHasher.HashDomains(exportSnapshot, domains);

        foreach (WorkspaceSyncDomain domain in domains)
        {
            if (!markers.TryGetValue(domain, out WorkspaceSyncMarker? marker))
            {
                continue;
            }

            bool databaseChanged = !string.Equals(canonicalHashes[domain], marker.CanonicalHash, StringComparison.Ordinal);
            bool exportChanged = !string.Equals(exportHashes[domain], marker.ExportHash, StringComparison.Ordinal);
            if (databaseChanged && exportChanged)
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.Conflict,
                    domain.ToString(),
                    domain.ToString(),
                    "sync-marker",
                    "database and export both changed since last marker",
                    "one side changed",
                    "Run storage-sync with an explicit reconciliation direction."));
            }
            else if (databaseChanged || exportChanged)
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.StaleExport,
                    domain.ToString(),
                    domain.ToString(),
                    "sync-marker",
                    databaseChanged ? "database changed since export marker" : "filesystem export changed since marker",
                    "marker hashes match current database and export",
                    databaseChanged ? "Run storage-export." : "Run storage-import or storage-export with explicit direction."));
            }
        }
    }

    private static async Task AddMissingExportFindingsAsync(
        RoadmapArtifacts artifacts,
        IReadOnlySet<WorkspaceSyncDomain> domains,
        WorkspaceFilesystemSnapshot snapshot,
        List<WorkspaceVerificationFinding> findings)
    {
        foreach (WorkspaceSyncDomain domain in domains)
        {
            foreach (string path in ExpectedExportPaths(snapshot, domain))
            {
                if (!await artifacts.ExistsAsync(path))
                {
                    findings.Add(Finding(
                        WorkspaceVerificationFindingKind.MissingExport,
                        domain.ToString(),
                        path,
                        "export-presence",
                        "missing",
                        "exported file exists",
                        "Run storage-export for the affected domain."));
                }
            }
        }
    }

    private static async Task AddRuntimePersistenceFindingsAsync(
        Repository repository,
        List<WorkspaceVerificationFinding> findings,
        CancellationToken cancellationToken)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);

        bool hasCanonicalResume = false;
        if (await TableExistsAsync(connection, "decision_session_resume", cancellationToken))
        {
            string? resumeJson = await ScalarStringAsync(
                connection,
                "SELECT document_json FROM decision_session_resume WHERE id = 1;",
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(resumeJson))
            {
                hasCanonicalResume = true;
                if (!IsValidDecisionResumeJson(resumeJson))
                {
                    findings.Add(Finding(
                        WorkspaceVerificationFindingKind.CorruptDomain,
                        "runtime-decision-resume",
                        "decision_session_resume",
                        "resume-state-schema",
                        "invalid",
                        "schemaVersion=1 with non-empty threadId",
                        "Clear the decision resume state or rerun the loop to repersist it."));
                }
            }
        }

        string legacyResumePath = Path.Combine(repository.Path, ".LoopRelay", "decision-session.json");
        if (hasCanonicalResume && File.Exists(legacyResumePath))
        {
            findings.Add(Finding(
                WorkspaceVerificationFindingKind.Conflict,
                "runtime-decision-resume",
                legacyResumePath,
                "legacy-file-authority",
                "legacy file exists beside canonical SQLite state",
                "SQLite is the only canonical decision resume store",
                "Delete the legacy decision-session.json file after confirming SQLite resume state is present."));
        }

        if (!await TableExistsAsync(connection, "session_telemetry_events", cancellationToken))
        {
            return;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT event_id, recorded_at, document_json, content_hash
            FROM session_telemetry_events
            ORDER BY event_id;
            """;
        long previousEventId = 0;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            long eventId = reader.GetInt64(0);
            string identity = $"session_telemetry_events:{eventId}";
            if (eventId <= previousEventId)
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.CorruptDomain,
                    "runtime-telemetry",
                    identity,
                    "event-order",
                    eventId.ToString(CultureInfo.InvariantCulture),
                    "strictly increasing event_id",
                    "Rebuild telemetry from a known-good database or discard corrupt runtime telemetry."));
            }

            previousEventId = eventId;
            string recordedAt = reader.GetString(1);
            string json = reader.GetString(2);
            string hash = reader.GetString(3);
            if (!DateTimeOffset.TryParse(
                    recordedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out _))
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.CorruptDomain,
                    "runtime-telemetry",
                    identity,
                    "recorded-at-format",
                    recordedAt,
                    "round-trip DateTimeOffset",
                    "Discard or repair the corrupt telemetry event row."));
            }

            if (!string.Equals(hash, Sha256(json), StringComparison.Ordinal))
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.CorruptDomain,
                    "runtime-telemetry",
                    identity,
                    "content-hash",
                    hash,
                    Sha256(json),
                    "Discard or repair the corrupt telemetry event row."));
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    findings.Add(Finding(
                        WorkspaceVerificationFindingKind.CorruptDomain,
                        "runtime-telemetry",
                        identity,
                        "event-json-shape",
                        document.RootElement.ValueKind.ToString(),
                        "JSON object",
                        "Discard or repair the corrupt telemetry event row."));
                }
            }
            catch (JsonException ex)
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.CorruptDomain,
                    "runtime-telemetry",
                    identity,
                    "event-json",
                    ex.Message,
                    "valid telemetry JSON object",
                    "Discard or repair the corrupt telemetry event row."));
            }
        }
    }

    private async Task AddUnresolvedPathFindingsAsync(
        RoadmapArtifacts artifacts,
        List<WorkspaceVerificationFinding> findings,
        CancellationToken cancellationToken)
    {
        foreach (string path in await _store.FindUnresolvedJournalOutputPathsAsync(artifacts, cancellationToken))
        {
            findings.Add(Finding(
                WorkspaceVerificationFindingKind.UnresolvedPath,
                "journal",
                path,
                "logical-artifact-resolution",
                "unresolved",
                "referenced logical artifact resolves",
                "Restore the missing artifact or fix the journal reference."));
        }
    }

    private static async Task AddCrossDomainIntegrityFindingsAsync(
        RoadmapArtifacts artifacts,
        WorkspaceFilesystemSnapshot snapshot,
        List<WorkspaceVerificationFinding> findings,
        CancellationToken cancellationToken)
    {
        ILogicalArtifactResolver resolver = RoadmapLogicalArtifactServices.CreateResolver(artifacts);
        var referencedMigratedPaths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string path in StateReferencePaths(snapshot.RoadmapState)
            .Concat(snapshot.TransitionJournal.SelectMany(JournalReferencePaths)))
        {
            string normalized = Normalize(path);
            if (!ShouldValidateReference(normalized))
            {
                continue;
            }

            if (IsMigratedArchivePath(normalized))
            {
                referencedMigratedPaths.Add(normalized);
            }

            LogicalArtifactResolutionResult result = await resolver.ResolveAsync(normalized, cancellationToken);
            if (!result.IsResolved)
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.InvalidReference,
                    "cross-domain",
                    normalized,
                    "state-journal-reference",
                    result.Status.ToString(),
                    "referenced logical path resolves",
                    "Restore the referenced artifact or repair persisted state/journal references."));
            }
        }

        foreach (SplitFamilyFilesystemSnapshot split in snapshot.SplitFamilies)
        {
            foreach (string childPath in split.Document.Family.ChildEpicPaths.Concat(split.Document.Family.DependencyOrder))
            {
                string normalized = Normalize(childPath);
                if (!await artifacts.ExistsAsync(normalized))
                {
                    findings.Add(Finding(
                        WorkspaceVerificationFindingKind.InvalidReference,
                        "split-lineage",
                        normalized,
                        "split-child-reference",
                        "missing",
                        "split child path exists",
                        "Restore the split child artifact or repair the split family record."));
                }
            }
        }

        foreach (ArtifactLifecycleEntryDto entry in snapshot.ArtifactLifecycle.Entries)
        {
            string normalized = Normalize(entry.Path);
            if (!ShouldValidateReference(normalized))
            {
                continue;
            }

            LogicalArtifactResolutionResult result = await resolver.ResolveAsync(normalized, cancellationToken);
            if (!result.IsResolved)
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.InvalidReference,
                    "artifact-lifecycle",
                    normalized,
                    "lifecycle-path-reference",
                    result.Status.ToString(),
                    "lifecycle path resolves",
                    "Restore the lifecycle artifact or remove the stale lifecycle row."));
            }
        }

        foreach (string duplicate in MigratedIdentityPaths(snapshot)
            .GroupBy(path => path, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            findings.Add(Finding(
                WorkspaceVerificationFindingKind.DuplicateIdentity,
                "cross-domain",
                duplicate,
                "migrated-identity-uniqueness",
                "duplicate",
                "unique migrated identity",
                "Repair duplicate migrated records before syncing."));
        }

        if (referencedMigratedPaths.Count > 0)
        {
            foreach (string orphan in snapshot.LoopHistories.Select(history => history.RelativePath)
                .Concat(snapshot.ExecutionEvidence.Select(evidence => evidence.RelativePath))
                .Select(Normalize)
                .Where(path => !referencedMigratedPaths.Contains(path))
                .Order(StringComparer.Ordinal))
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.OrphanedArtifact,
                    "cross-domain",
                    orphan,
                    "migrated-record-association",
                    "unreferenced",
                    "referenced by state, journal, or workflow context",
                    "Archive, reference, or remove the orphaned migrated record."));
            }
        }
    }

    private static IEnumerable<string> StateReferencePaths(RoadmapStatePersistenceDocument? state)
    {
        if (state is null)
        {
            yield break;
        }

        foreach (string path in RoadmapTransitionPersistence.ParseOutputEvidencePaths(state.LastTransition.Output))
        {
            yield return path;
        }

        foreach (string path in state.TransitionIntent.EvidencePaths)
        {
            yield return path;
        }

        foreach (string path in state.ActiveArtifacts.Select(artifact => artifact.Path))
        {
            yield return path;
        }
    }

    private static IEnumerable<string> JournalReferencePaths(TransitionJournalRecord record)
    {
        foreach (string path in record.OutputPaths)
        {
            yield return path;
        }

        foreach (string path in record.InputArtifactHashes.Keys)
        {
            yield return path;
        }

        if (record.InputSnapshot is not null)
        {
            foreach (string path in record.InputSnapshot.ArtifactInputs.Select(input => input.Path))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> MigratedIdentityPaths(WorkspaceFilesystemSnapshot snapshot)
    {
        yield return RoadmapArtifactPaths.DecisionLedgerJson;
        if (snapshot.RoadmapState is not null)
        {
            yield return RoadmapArtifactPaths.StateJson;
        }

        yield return RoadmapArtifactPaths.LifecycleJson;
        foreach (SplitFamilyFilesystemSnapshot split in snapshot.SplitFamilies)
        {
            yield return split.RelativePath;
        }

        yield return RoadmapArtifactPaths.ExecutionPreparationManifest;
        yield return RoadmapArtifactPaths.SelectionProvenanceManifest;
        yield return RoadmapArtifactPaths.ProjectionsManifestJson;
        if (snapshot.TransitionJournal.Count > 0)
        {
            yield return RoadmapArtifactPaths.TransitionJournal;
        }

        foreach (LoopHistoryFilesystemSnapshot history in snapshot.LoopHistories)
        {
            yield return history.RelativePath;
        }

        foreach (ExecutionEvidenceFilesystemSnapshot evidence in snapshot.ExecutionEvidence)
        {
            yield return evidence.RelativePath;
        }
    }

    private static bool ShouldValidateReference(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !string.Equals(path, "None", StringComparison.OrdinalIgnoreCase);

    private static bool IsMigratedArchivePath(string path) =>
        path.StartsWith(RoadmapArtifactPaths.ExecutionEvidenceDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(OrchestrationArtifactPaths.DecisionsDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(OrchestrationArtifactPaths.HandoffsDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(OrchestrationArtifactPaths.DeltasDirectory + "/", StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim().TrimStart('/');

    private static async Task AddArchiveFindingsAsync(
        RoadmapArtifacts artifacts,
        List<WorkspaceVerificationFinding> findings,
        CancellationToken cancellationToken)
    {
        var recovery = new CompletedEpicArchiveRecoveryService(artifacts.Store, artifacts.Repository);
        IReadOnlyList<string> directories = await artifacts.ListDirectoriesAsync(RoadmapArtifactPaths.CompletedEpicsDirectory);
        foreach (string directory in directories.Order(StringComparer.Ordinal))
        {
            if (!int.TryParse(Path.GetFileName(directory), out int index))
            {
                continue;
            }

            try
            {
                _ = await recovery.LoadAsync(index, cancellationToken: cancellationToken);
            }
            catch (Exception exception) when (exception is InvalidOperationException or JsonException)
            {
                findings.Add(Finding(
                    WorkspaceVerificationFindingKind.UnrecoverableArchive,
                    "archive",
                    directory,
                    "archive-recovery",
                    exception.Message,
                    "recoverable completed epic archive",
                    "Repair archive metadata or regenerate archive exports."));
            }
        }
    }

    private static async Task AddWorkflowRecoveryFindingsAsync(
        RoadmapArtifacts artifacts,
        List<WorkspaceVerificationFinding> findings,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkflowRecoveryFinding> recoveryFindings =
            await new WorkflowPersistenceCoordinator().ClassifyAsync(artifacts.Repository, cancellationToken);
        foreach (WorkflowRecoveryFinding recovery in recoveryFindings)
        {
            findings.Add(Finding(
                recovery.Classification == WorkflowRecoveryClassification.RetryablePartial
                    ? WorkspaceVerificationFindingKind.MutationRequired
                    : WorkspaceVerificationFindingKind.CorruptDomain,
                "workflow",
                recovery.TransactionId,
                "workflow-transaction-marker",
                recovery.Reason,
                "completed workflow transaction marker",
                "Retry or explicitly repair the interrupted workflow persistence phase."));
        }
    }

    private async Task AddRoundTripFindingsAsync(
        Repository sourceRepository,
        IReadOnlySet<WorkspaceSyncDomain> domains,
        WorkspaceFilesystemSnapshot snapshot,
        List<WorkspaceVerificationFinding> findings,
        CancellationToken cancellationToken)
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "looprelay-verify", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            var tempRepository = new Repository
            {
                Id = Guid.NewGuid(),
                Name = sourceRepository.Name,
                Path = tempRoot,
            };
            var tempArtifacts = new RoadmapArtifacts(new FileSystemArtifactStore(), tempRepository);
            await _filesystemSnapshots.ExportAsync(tempArtifacts, snapshot);
            var tempStore = new WorkspaceSqliteStore();
            await tempStore.ImportAsync(tempArtifacts, domains, cancellationToken);
            WorkspaceFilesystemSnapshot roundTrip = await tempStore.ReadSnapshotAsync(tempRepository, cancellationToken);
            if (_roundTripSnapshotMutator is not null)
            {
                roundTrip = _roundTripSnapshotMutator(roundTrip);
            }

            IReadOnlyDictionary<WorkspaceSyncDomain, string> sourceHashes =
                WorkspaceSyncSnapshotHasher.HashDomains(snapshot, domains);
            IReadOnlyDictionary<WorkspaceSyncDomain, string> roundTripHashes =
                WorkspaceSyncSnapshotHasher.HashDomains(roundTrip, domains);
            foreach (WorkspaceSyncDomain domain in domains)
            {
                if (!string.Equals(sourceHashes[domain], roundTripHashes[domain], StringComparison.Ordinal))
                {
                    findings.Add(Finding(
                        WorkspaceVerificationFindingKind.NondeterministicRoundTrip,
                        domain.ToString(),
                        domain.ToString(),
                        "export-import-roundtrip",
                        roundTripHashes[domain],
                        sourceHashes[domain],
                        "Fix nondeterministic serializer or importer behavior."));
                }
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private async Task<WorkspaceVerificationResult> FinishWithMutationGuardAsync(
        RoadmapArtifacts artifacts,
        IReadOnlySet<WorkspaceSyncDomain> domains,
        string? beforeDatabaseHash,
        List<WorkspaceVerificationFinding> findings)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(artifacts.Repository);
        string? afterDatabaseHash = File.Exists(databasePath) ? FileHash(databasePath) : null;
        if (!string.Equals(beforeDatabaseHash, afterDatabaseHash, StringComparison.Ordinal))
        {
            findings.Add(Finding(
                WorkspaceVerificationFindingKind.MutationRequired,
                "database",
                databasePath,
                "read-only-mutation-guard",
                afterDatabaseHash ?? "missing",
                beforeDatabaseHash ?? "missing",
                "Verification must be read-only; investigate unexpected mutation."));
        }

        if (File.Exists(databasePath))
        {
            try
            {
                WorkspaceFilesystemSnapshot snapshot = await _store.ReadSnapshotAsync(artifacts.Repository);
                foreach (string path in domains.SelectMany(domain => ExpectedExportPaths(snapshot, domain)).Distinct(StringComparer.Ordinal))
                {
                    _ = await artifacts.ExistsAsync(path);
                }
            }
            catch
            {
                // Other findings already report corrupt state; the mutation guard stays best-effort for exports.
            }
        }

        return new WorkspaceVerificationResult(findings.Count == 0, findings);
    }

    private static IEnumerable<string> ExpectedExportPaths(WorkspaceFilesystemSnapshot snapshot, WorkspaceSyncDomain domain)
    {
        return domain switch
        {
            WorkspaceSyncDomain.Core => CoreExportPaths(snapshot),
            WorkspaceSyncDomain.Metadata =>
            [
                RoadmapArtifactPaths.ExecutionPreparationManifest,
                RoadmapArtifactPaths.SelectionProvenanceManifest,
                RoadmapArtifactPaths.ProjectionsManifestJson,
            ],
            WorkspaceSyncDomain.Journal => snapshot.TransitionJournal.Count == 0 ? [] : [RoadmapArtifactPaths.TransitionJournal],
            WorkspaceSyncDomain.LoopHistories => snapshot.LoopHistories.Select(history => history.RelativePath),
            WorkspaceSyncDomain.ExecutionEvidence => snapshot.ExecutionEvidence.Select(evidence => evidence.RelativePath),
            _ => [],
        };
    }

    private static IEnumerable<string> CoreExportPaths(WorkspaceFilesystemSnapshot snapshot)
    {
        yield return RoadmapArtifactPaths.DecisionLedgerJson;
        if (snapshot.RoadmapState is not null)
        {
            yield return RoadmapArtifactPaths.StateJson;
        }

        yield return RoadmapArtifactPaths.LifecycleJson;
        foreach (SplitFamilyFilesystemSnapshot split in snapshot.SplitFamilies)
        {
            yield return split.RelativePath;
        }
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<string?> ScalarStringAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    private static bool IsValidDecisionResumeJson(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("schemaVersion", out JsonElement schema) &&
                schema.TryGetInt32(out int schemaVersion) &&
                schemaVersion == 1 &&
                root.TryGetProperty("threadId", out JsonElement threadId) &&
                !string.IsNullOrWhiteSpace(threadId.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static WorkspaceVerificationFinding Finding(
        WorkspaceVerificationFindingKind kind,
        string domain,
        string identity,
        string rule,
        string current,
        string expected,
        string action) =>
        new(kind, domain, identity, rule, "error", current, expected, action);

    private static string FileHash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
