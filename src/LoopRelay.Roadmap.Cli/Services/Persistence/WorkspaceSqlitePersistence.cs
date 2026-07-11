using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;
using LoopRelay.Roadmap.Cli.Models.ProjectionManifests;
using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Models.Transitions;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal enum WorkspaceDatabaseIntegrityStatus
{
    Missing,
    ValidEmpty,
    ValidImported,
    ValidCanonical,
    Corrupt,
    UnsupportedSchema,
    IncompatiblePartialState,
}

internal enum WorkspaceStorageResultCategory
{
    Initialized,
    Imported,
    Exported,
    Unchanged,
    StaleExport,
    Conflict,
    UnsupportedVersion,
    ValidationFailure,
    VerificationFailed,
}

internal sealed record WorkspaceDatabaseIntegrityResult(
    WorkspaceDatabaseIntegrityStatus Status,
    string DatabasePath,
    string Message);

internal sealed record WorkspaceSqliteOperationResult(
    WorkspaceStorageResultCategory Category,
    WorkspaceDatabaseIntegrityStatus Status,
    string DatabasePath,
    string Message);

internal static class WorkspaceDatabaseLocator
{
    public const string RelativeDatabasePath = LoopRelayWorkspaceDatabase.RelativeDatabasePath;

    public static string Resolve(Repository repository) =>
        LoopRelayWorkspaceDatabase.Resolve(repository);
}

internal sealed class WorkspaceSqliteStore
{
    public const int CurrentSchemaVersion = LoopRelayWorkspaceDatabase.CurrentSchemaVersion;

    private const string PersistenceStateKey = "persistence_state";
    private const string EmptyPersistenceState = "empty";
    private const string ImportedPersistenceState = "imported";
    private const string CanonicalPersistenceState = "canonical";

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions JournalJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkspaceSqliteOperationResult> InitializeAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        await using SqliteConnection connection = OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        await SetWorkspaceMetadataAsync(
            connection,
            null,
            PersistenceStateKey,
            EmptyPersistenceState,
            cancellationToken);

        return new WorkspaceSqliteOperationResult(
            WorkspaceStorageResultCategory.Initialized,
            WorkspaceDatabaseIntegrityStatus.ValidEmpty,
            databasePath,
            "SQLite workspace database initialized.");
    }

    public Task<WorkspaceSqliteOperationResult> ImportAsync(
        RoadmapArtifacts artifacts,
        CancellationToken cancellationToken = default) =>
        ImportAsync(artifacts, domains: null, cancellationToken);

    public async Task<WorkspaceSqliteOperationResult> ImportAsync(
        RoadmapArtifacts artifacts,
        IReadOnlySet<WorkspaceSyncDomain>? domains,
        CancellationToken cancellationToken = default)
    {
        IReadOnlySet<WorkspaceSyncDomain> effectiveDomains = WorkspaceSyncDomains.Effective(domains);
        WorkspaceFilesystemSnapshot sourceSnapshot = await new WorkspaceFilesystemSnapshotStore().ImportAsync(artifacts);
        IReadOnlyDictionary<WorkspaceSyncDomain, string> sourceHashes =
            WorkspaceSyncSnapshotHasher.HashDomains(sourceSnapshot, effectiveDomains);
        string databasePath = WorkspaceDatabaseLocator.Resolve(artifacts.Repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        await using SqliteConnection connection = OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await ClearImportedTablesAsync(connection, transaction, effectiveDomains, cancellationToken);
        await InsertSnapshotAsync(connection, transaction, sourceSnapshot, effectiveDomains, cancellationToken);
        await SetWorkspaceMetadataAsync(
            connection,
            transaction,
            PersistenceStateKey,
            ImportedPersistenceState,
            cancellationToken);
        await SetWorkspaceMetadataAsync(
            connection,
            transaction,
            "imported_at",
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            cancellationToken);

        WorkspaceFilesystemSnapshot importedSnapshot = await ReadSnapshotAsync(connection, transaction, cancellationToken);
        IReadOnlyDictionary<WorkspaceSyncDomain, string> importedHashes =
            WorkspaceSyncSnapshotHasher.HashDomains(importedSnapshot, effectiveDomains);
        if (!sourceHashes.OrderBy(pair => pair.Key).SequenceEqual(importedHashes.OrderBy(pair => pair.Key)))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new WorkspaceSqliteOperationResult(
                WorkspaceStorageResultCategory.ValidationFailure,
                WorkspaceDatabaseIntegrityStatus.Corrupt,
                databasePath,
                "Imported SQLite snapshot did not match the filesystem snapshot.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new WorkspaceSqliteOperationResult(
            WorkspaceStorageResultCategory.Imported,
            WorkspaceDatabaseIntegrityStatus.ValidImported,
            databasePath,
            $"Filesystem snapshot imported into SQLite for {WorkspaceSyncDomains.Describe(effectiveDomains)}.");
    }

    public async Task<WorkspaceDatabaseIntegrityResult> ValidateAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(repository);
        if (!File.Exists(databasePath))
        {
            return new WorkspaceDatabaseIntegrityResult(
                WorkspaceDatabaseIntegrityStatus.Missing,
                databasePath,
                "SQLite workspace database is missing.");
        }

        try
        {
            await using SqliteConnection connection = OpenReadOnly(databasePath);
            await connection.OpenAsync(cancellationToken);
            int schemaVersion = await ReadSchemaVersionAsync(connection, null, cancellationToken);
            if (schemaVersion != CurrentSchemaVersion)
            {
                return new WorkspaceDatabaseIntegrityResult(
                    WorkspaceDatabaseIntegrityStatus.UnsupportedSchema,
                    databasePath,
                    $"Unsupported SQLite schema version `{schemaVersion}`; expected `{CurrentSchemaVersion}`.");
            }

            _ = await ReadSnapshotAsync(connection, null, cancellationToken);
            string? state = await ReadWorkspaceMetadataAsync(
                connection,
                null,
                PersistenceStateKey,
                cancellationToken);
            bool hasRows = await HasDomainRowsAsync(connection, cancellationToken);
            WorkspaceDatabaseIntegrityStatus status = state switch
            {
                EmptyPersistenceState when !hasRows => WorkspaceDatabaseIntegrityStatus.ValidEmpty,
                ImportedPersistenceState => WorkspaceDatabaseIntegrityStatus.ValidImported,
                CanonicalPersistenceState => WorkspaceDatabaseIntegrityStatus.ValidCanonical,
                _ => WorkspaceDatabaseIntegrityStatus.IncompatiblePartialState,
            };

            return new WorkspaceDatabaseIntegrityResult(
                status,
                databasePath,
                $"SQLite workspace database classified as {status}.");
        }
        catch (SqliteException exception)
        {
            return new WorkspaceDatabaseIntegrityResult(
                WorkspaceDatabaseIntegrityStatus.Corrupt,
                databasePath,
                exception.Message);
        }
        catch (JsonException exception)
        {
            return new WorkspaceDatabaseIntegrityResult(
                WorkspaceDatabaseIntegrityStatus.Corrupt,
                databasePath,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return new WorkspaceDatabaseIntegrityResult(
                WorkspaceDatabaseIntegrityStatus.Corrupt,
                databasePath,
                exception.Message);
        }
    }

    public async Task<WorkspaceFilesystemSnapshot> ReadSnapshotAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(repository);
        await using SqliteConnection connection = OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        int schemaVersion = await ReadSchemaVersionAsync(connection, null, cancellationToken);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported SQLite schema version `{schemaVersion}`; expected `{CurrentSchemaVersion}`.");
        }

        return await ReadSnapshotAsync(connection, null, cancellationToken);
    }

    public async Task<WorkspaceSqliteOperationResult> ExportCoreAsync(
        RoadmapArtifacts artifacts,
        CancellationToken cancellationToken = default)
    {
        WorkspaceFilesystemSnapshot snapshot = await ReadSnapshotAsync(artifacts.Repository, cancellationToken);
        await artifacts.WriteAsync(
            RoadmapArtifactPaths.DecisionLedgerJson,
            JsonSerializer.Serialize(snapshot.DecisionLedger, RoadmapJson.Options) + Environment.NewLine);

        if (snapshot.RoadmapState is not null)
        {
            await artifacts.WriteAsync(
                RoadmapArtifactPaths.StateJson,
                JsonSerializer.Serialize(snapshot.RoadmapState, RoadmapJson.Options) + Environment.NewLine);
        }

        await artifacts.WriteAsync(
            RoadmapArtifactPaths.LifecycleJson,
            JsonSerializer.Serialize(snapshot.ArtifactLifecycle, RoadmapJson.Options) + Environment.NewLine);

        foreach (SplitFamilyFilesystemSnapshot split in snapshot.SplitFamilies.OrderBy(split => split.FamilyId, StringComparer.Ordinal))
        {
            await artifacts.WriteAsync(
                split.RelativePath,
                JsonSerializer.Serialize(split.Document, RoadmapJson.Options) + Environment.NewLine);
        }

        return new WorkspaceSqliteOperationResult(
            WorkspaceStorageResultCategory.Exported,
            WorkspaceDatabaseIntegrityStatus.ValidImported,
            WorkspaceDatabaseLocator.Resolve(artifacts.Repository),
            "Core SQLite-backed roadmap state exported to filesystem equivalents.");
    }

    public async Task<WorkspaceSqliteOperationResult> ExportMetadataAsync(
        RoadmapArtifacts artifacts,
        CancellationToken cancellationToken = default)
    {
        WorkspaceFilesystemSnapshot snapshot = await ReadSnapshotAsync(artifacts.Repository, cancellationToken);
        await artifacts.WriteAsync(
            RoadmapArtifactPaths.ExecutionPreparationManifest,
            JsonSerializer.Serialize(snapshot.ExecutionPreparationManifest, SnapshotJsonOptions) + Environment.NewLine);
        await artifacts.WriteAsync(
            RoadmapArtifactPaths.SelectionProvenanceManifest,
            JsonSerializer.Serialize(snapshot.SelectionProvenanceManifest, SnapshotJsonOptions) + Environment.NewLine);
        await artifacts.WriteAsync(
            RoadmapArtifactPaths.ProjectionsManifestJson,
            JsonSerializer.Serialize(snapshot.ProjectionManifest, RoadmapJson.Options) + Environment.NewLine);

        return new WorkspaceSqliteOperationResult(
            WorkspaceStorageResultCategory.Exported,
            WorkspaceDatabaseIntegrityStatus.ValidImported,
            WorkspaceDatabaseLocator.Resolve(artifacts.Repository),
            "SQLite-backed provenance and projection metadata exported to filesystem equivalents.");
    }

    public async Task<WorkspaceSqliteOperationResult> ExportJournalAsync(
        RoadmapArtifacts artifacts,
        CancellationToken cancellationToken = default)
    {
        WorkspaceFilesystemSnapshot snapshot = await ReadSnapshotAsync(artifacts.Repository, cancellationToken);
        if (snapshot.TransitionJournal.Count > 0)
        {
            string journal = string.Join(
                Environment.NewLine,
                snapshot.TransitionJournal.Select(record => JsonSerializer.Serialize(record, JournalJsonOptions))) +
                Environment.NewLine;
            await artifacts.WriteAsync(RoadmapArtifactPaths.TransitionJournal, journal);
        }

        return new WorkspaceSqliteOperationResult(
            WorkspaceStorageResultCategory.Exported,
            WorkspaceDatabaseIntegrityStatus.ValidImported,
            WorkspaceDatabaseLocator.Resolve(artifacts.Repository),
            "SQLite-backed transition journal exported to JSONL.");
    }

    public async Task<WorkspaceSqliteOperationResult> ExportLoopHistoriesAsync(
        RoadmapArtifacts artifacts,
        CancellationToken cancellationToken = default)
    {
        WorkspaceFilesystemSnapshot snapshot = await ReadSnapshotAsync(artifacts.Repository, cancellationToken);
        foreach (LoopHistoryFilesystemSnapshot history in snapshot.LoopHistories
            .OrderBy(history => history.Kind)
            .ThenBy(history => history.Sequence))
        {
            await artifacts.WriteAsync(history.RelativePath, history.Body);
        }

        return new WorkspaceSqliteOperationResult(
            WorkspaceStorageResultCategory.Exported,
            WorkspaceDatabaseIntegrityStatus.ValidImported,
            WorkspaceDatabaseLocator.Resolve(artifacts.Repository),
            "SQLite-backed loop histories exported to numbered markdown files.");
    }

    public async Task<WorkspaceSqliteOperationResult> ExportExecutionEvidenceAsync(
        RoadmapArtifacts artifacts,
        CancellationToken cancellationToken = default)
    {
        WorkspaceFilesystemSnapshot snapshot = await ReadSnapshotAsync(artifacts.Repository, cancellationToken);
        foreach (ExecutionEvidenceFilesystemSnapshot evidence in snapshot.ExecutionEvidence
            .OrderBy(evidence => evidence.Stem, StringComparer.Ordinal)
            .ThenBy(evidence => evidence.Sequence))
        {
            await artifacts.WriteAsync(evidence.RelativePath, evidence.Body);
        }

        return new WorkspaceSqliteOperationResult(
            WorkspaceStorageResultCategory.Exported,
            WorkspaceDatabaseIntegrityStatus.ValidImported,
            WorkspaceDatabaseLocator.Resolve(artifacts.Repository),
            "SQLite-backed execution evidence exported to numbered markdown files.");
    }

    public async Task<IReadOnlyList<string>> FindUnresolvedJournalOutputPathsAsync(
        RoadmapArtifacts artifacts,
        CancellationToken cancellationToken = default)
    {
        WorkspaceFilesystemSnapshot snapshot = await ReadSnapshotAsync(artifacts.Repository, cancellationToken);
        ILogicalArtifactResolver resolver = RoadmapLogicalArtifactServices.CreateResolver(artifacts);
        var unresolved = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string path in snapshot.TransitionJournal.SelectMany(record => record.OutputPaths))
        {
            if (string.IsNullOrWhiteSpace(path) ||
                string.Equals(path, "None", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            LogicalArtifactResolutionResult result = await resolver.ResolveAsync(path, cancellationToken);
            if (!result.IsResolved)
            {
                unresolved.Add(path);
            }
        }

        return unresolved.ToArray();
    }

    public async Task<IReadOnlyDictionary<WorkspaceSyncDomain, WorkspaceSyncMarker>> ReadSyncMarkersAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(repository);
        if (!File.Exists(databasePath))
        {
            return new Dictionary<WorkspaceSyncDomain, WorkspaceSyncMarker>();
        }

        await using SqliteConnection connection = OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        int schemaVersion = await ReadSchemaVersionAsync(connection, null, cancellationToken);
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported SQLite schema version `{schemaVersion}`; expected `{CurrentSchemaVersion}`.");
        }

        var markers = new Dictionary<WorkspaceSyncDomain, WorkspaceSyncMarker>();
        await using SqliteCommand command = CreateCommand(
            connection,
            null,
            "SELECT domain, canonical_hash, export_hash, generation, updated_at FROM sync_markers ORDER BY domain;");
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string domainName = reader.GetString(0);
            if (!Enum.TryParse(domainName, ignoreCase: false, out WorkspaceSyncDomain domain))
            {
                throw new InvalidOperationException($"Unsupported sync marker domain `{domainName}`.");
            }

            markers[domain] = new WorkspaceSyncMarker(
                domain,
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt64(3),
                ParseDate(reader.GetString(4)));
        }

        return markers;
    }

    public async Task WriteSyncMarkersAsync(
        Repository repository,
        IReadOnlyList<WorkspaceSyncMarkerUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        if (updates.Count == 0)
        {
            return;
        }

        string databasePath = WorkspaceDatabaseLocator.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        string updatedAt = Format(DateTimeOffset.UtcNow);
        foreach (WorkspaceSyncMarkerUpdate update in updates)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO sync_markers (domain, canonical_hash, export_hash, generation, updated_at)
                VALUES ($domain, $canonical_hash, $export_hash, 1, $updated_at)
                ON CONFLICT(domain) DO UPDATE SET
                    canonical_hash = excluded.canonical_hash,
                    export_hash = excluded.export_hash,
                    generation = sync_markers.generation + 1,
                    updated_at = excluded.updated_at;
                """,
                cancellationToken,
                ("$domain", update.Domain.ToString()),
                ("$canonical_hash", update.CanonicalHash),
                ("$export_hash", update.ExportHash),
                ("$updated_at", updatedAt));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    internal static async Task EnsureSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
    }

    private static async Task ClearImportedTablesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlySet<WorkspaceSyncDomain> domains,
        CancellationToken cancellationToken)
    {
        var tables = new List<string>();
        if (domains.Count == WorkspaceSyncDomains.All.Count)
        {
            tables.AddRange(
            [
                "sync_markers",
                "workflow_transactions",
                "completed_epic_records",
                "completed_epic_archives",
            ]);
        }

        if (domains.Contains(WorkspaceSyncDomain.ExecutionEvidence))
        {
            tables.Add("execution_evidence");
        }

        if (domains.Contains(WorkspaceSyncDomain.LoopHistories))
        {
            tables.Add("loop_history");
        }

        if (domains.Contains(WorkspaceSyncDomain.Journal))
        {
            tables.Add("transition_journal");
        }

        if (domains.Contains(WorkspaceSyncDomain.Metadata))
        {
            tables.AddRange(
            [
                "projection_manifest_entries",
                "selection_provenance_manifest",
                "execution_preparation_manifest",
            ]);
        }

        if (domains.Contains(WorkspaceSyncDomain.Core))
        {
            tables.AddRange(
            [
                "split_family_dependency_order",
                "split_family_children",
                "split_families",
                "artifact_lifecycle",
                "roadmap_state",
                "decision_ledger",
            ]);
        }

        foreach (string table in tables)
        {
            await ExecuteAsync(connection, transaction, $"DELETE FROM {table};", cancellationToken);
        }
    }

    private static async Task InsertSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        WorkspaceFilesystemSnapshot snapshot,
        IReadOnlySet<WorkspaceSyncDomain> domains,
        CancellationToken cancellationToken)
    {
        if (domains.Contains(WorkspaceSyncDomain.Core))
        {
            foreach (DecisionLedgerEntryDto entry in snapshot.DecisionLedger.Entries)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO decision_ledger (
                        decision_id, timestamp, state, transition, prompt, projection_path,
                        input_paths_json, output_paths_json, decision, confidence, rationale_excerpt)
                    VALUES (
                        $decision_id, $timestamp, $state, $transition, $prompt, $projection_path,
                        $input_paths_json, $output_paths_json, $decision, $confidence, $rationale_excerpt);
                    """,
                    cancellationToken,
                    ("$decision_id", entry.DecisionId),
                    ("$timestamp", Format(entry.Timestamp)),
                    ("$state", entry.State.ToString()),
                    ("$transition", entry.Transition),
                    ("$prompt", entry.Prompt),
                    ("$projection_path", entry.ProjectionPath),
                    ("$input_paths_json", JsonSerializer.Serialize(entry.InputArtifactPaths, SnapshotJsonOptions)),
                    ("$output_paths_json", JsonSerializer.Serialize(entry.OutputArtifactPaths, SnapshotJsonOptions)),
                    ("$decision", entry.Decision),
                    ("$confidence", entry.Confidence),
                    ("$rationale_excerpt", entry.RationaleExcerpt));
            }

            if (snapshot.RoadmapState is not null)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "INSERT INTO roadmap_state (id, document_json, updated_at) VALUES (1, $document_json, $updated_at);",
                    cancellationToken,
                    ("$document_json", JsonSerializer.Serialize(snapshot.RoadmapState, RoadmapJson.Options)),
                    ("$updated_at", Format(DateTimeOffset.UtcNow)));
            }

            foreach (ArtifactLifecycleEntryDto entry in snapshot.ArtifactLifecycle.Entries)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO artifact_lifecycle (path_key, path, state, updated_at, notes)
                    VALUES ($path_key, $path, $state, $updated_at, $notes);
                    """,
                    cancellationToken,
                    ("$path_key", entry.Path.ToUpperInvariant()),
                    ("$path", entry.Path),
                    ("$state", entry.State.ToString()),
                    ("$updated_at", Format(entry.UpdatedAt)),
                    ("$notes", entry.Notes));
            }

            foreach (SplitFamilyFilesystemSnapshot split in snapshot.SplitFamilies)
            {
                SplitFamilyDto family = split.Document.Family;
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO split_families (
                        family_id, proposal, selected_child, selected_child_rationale, created_at)
                    VALUES ($family_id, $proposal, $selected_child, $selected_child_rationale, $created_at);
                    """,
                    cancellationToken,
                    ("$family_id", family.FamilyId),
                    ("$proposal", family.Proposal),
                    ("$selected_child", family.SelectedChildPath),
                    ("$selected_child_rationale", family.SelectedChildRationale),
                    ("$created_at", Format(family.CreatedAt)));

                await InsertOrderedPathsAsync(
                    connection,
                    transaction,
                    "split_family_children",
                    family.FamilyId,
                    family.ChildEpicPaths,
                    "child_path",
                    cancellationToken);
                await InsertOrderedPathsAsync(
                    connection,
                    transaction,
                    "split_family_dependency_order",
                    family.FamilyId,
                    family.DependencyOrder,
                    "child_path",
                    cancellationToken);
            }
        }

        if (domains.Contains(WorkspaceSyncDomain.Metadata))
        {
            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO execution_preparation_manifest (id, document_json, updated_at) VALUES (1, $document_json, $updated_at);",
                cancellationToken,
                ("$document_json", JsonSerializer.Serialize(snapshot.ExecutionPreparationManifest, SnapshotJsonOptions)),
                ("$updated_at", Format(DateTimeOffset.UtcNow)));

            await ExecuteAsync(
                connection,
                transaction,
                "INSERT INTO selection_provenance_manifest (id, document_json, updated_at) VALUES (1, $document_json, $updated_at);",
                cancellationToken,
                ("$document_json", JsonSerializer.Serialize(snapshot.SelectionProvenanceManifest, SnapshotJsonOptions)),
                ("$updated_at", Format(DateTimeOffset.UtcNow)));

            foreach (ProjectionManifestEntryDto entry in snapshot.ProjectionManifest.Entries)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    "INSERT INTO projection_manifest_entries (runtime_prompt, document_json, updated_at) VALUES ($runtime_prompt, $document_json, $updated_at);",
                    cancellationToken,
                    ("$runtime_prompt", entry.RuntimePromptName),
                    ("$document_json", JsonSerializer.Serialize(entry, RoadmapJson.Options)),
                    ("$updated_at", Format(DateTimeOffset.UtcNow)));
            }
        }

        if (domains.Contains(WorkspaceSyncDomain.Journal))
        {
            foreach (TransitionJournalRecord record in snapshot.TransitionJournal)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO transition_journal (
                        correlation_id, event_name, recorded_at, from_state, to_state, transition,
                        projection_path, prompt_contract, input_hashes_json, output_paths_json,
                        duration_milliseconds, retry_count, result, decision, error, input_snapshot_json)
                    VALUES (
                        $correlation_id, $event_name, $recorded_at, $from_state, $to_state, $transition,
                        $projection_path, $prompt_contract, $input_hashes_json, $output_paths_json,
                        $duration_milliseconds, 0, $result, $decision, $error, $input_snapshot_json);
                    """,
                    cancellationToken,
                    ("$correlation_id", record.CorrelationId),
                    ("$event_name", record.Event),
                    ("$recorded_at", Format(record.Timestamp)),
                    ("$from_state", record.PreviousState.ToString()),
                    ("$to_state", record.AttemptedState.ToString()),
                    ("$transition", record.Prompt),
                    ("$projection_path", record.Projection),
                    ("$prompt_contract", record.PromptContractKey),
                    ("$input_hashes_json", JsonSerializer.Serialize(record.InputArtifactHashes, SnapshotJsonOptions)),
                    ("$output_paths_json", JsonSerializer.Serialize(record.OutputPaths, SnapshotJsonOptions)),
                    ("$duration_milliseconds", record.DurationMilliseconds),
                    ("$result", record.Result),
                    ("$decision", record.ParserDecision),
                    ("$error", record.ErrorMessage),
                    ("$input_snapshot_json", record.InputSnapshot is null ? null : JsonSerializer.Serialize(record.InputSnapshot, SnapshotJsonOptions)));
            }
        }

        if (domains.Contains(WorkspaceSyncDomain.LoopHistories))
        {
            foreach (LoopHistoryFilesystemSnapshot history in snapshot.LoopHistories)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO loop_history (kind, sequence, logical_path, body, content_hash, created_at)
                    VALUES ($kind, $sequence, $logical_path, $body, $content_hash, $created_at);
                    """,
                    cancellationToken,
                    ("$kind", history.Kind.ToString()),
                    ("$sequence", history.Sequence),
                    ("$logical_path", history.RelativePath),
                    ("$body", history.Body),
                    ("$content_hash", Sha256(history.Body)),
                    ("$created_at", Format(DateTimeOffset.UtcNow)));
            }
        }

        if (domains.Contains(WorkspaceSyncDomain.ExecutionEvidence))
        {
            foreach (ExecutionEvidenceFilesystemSnapshot evidence in snapshot.ExecutionEvidence)
            {
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO execution_evidence (
                        logical_path, stem, sequence, body, content_hash, created_at, writer, metadata_json)
                    VALUES ($logical_path, $stem, $sequence, $body, $content_hash, $created_at, NULL, '{}');
                    """,
                    cancellationToken,
                    ("$logical_path", evidence.RelativePath),
                    ("$stem", evidence.Stem),
                    ("$sequence", evidence.Sequence),
                    ("$body", evidence.Body),
                    ("$content_hash", Sha256(evidence.Body)),
                    ("$created_at", Format(DateTimeOffset.UtcNow)));
            }
        }
    }

    private static async Task<WorkspaceFilesystemSnapshot> ReadSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        return new WorkspaceFilesystemSnapshot(
            await ReadDecisionLedgerAsync(connection, transaction, cancellationToken),
            await ReadRoadmapStateAsync(connection, transaction, cancellationToken),
            await ReadArtifactLifecycleAsync(connection, transaction, cancellationToken),
            await ReadSplitFamiliesAsync(connection, transaction, cancellationToken),
            await ReadExecutionPreparationManifestAsync(connection, transaction, cancellationToken),
            await ReadSelectionProvenanceManifestAsync(connection, transaction, cancellationToken),
            await ReadProjectionManifestAsync(connection, transaction, cancellationToken),
            await ReadTransitionJournalAsync(connection, transaction, cancellationToken),
            await ReadLoopHistoriesAsync(connection, transaction, cancellationToken),
            await ReadExecutionEvidenceAsync(connection, transaction, cancellationToken));
    }

    private static async Task<DecisionLedgerPersistenceDocument> ReadDecisionLedgerAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var entries = new List<DecisionLedgerEntryDto>();
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            """
            SELECT decision_id, timestamp, state, transition, prompt, projection_path,
                   input_paths_json, output_paths_json, decision, confidence, rationale_excerpt
            FROM decision_ledger
            ORDER BY decision_id;
            """);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new DecisionLedgerEntryDto(
                reader.GetString(0),
                ParseDate(reader.GetString(1)),
                ParseEnum<RoadmapState>(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                Deserialize<IReadOnlyList<string>>(reader.GetString(6)),
                Deserialize<IReadOnlyList<string>>(reader.GetString(7)),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10)));
        }

        var document = new DecisionLedgerPersistenceDocument(
            DecisionLedgerPersistenceDocument.CurrentSchemaVersion,
            entries);
        ThrowIfInvalid("decision ledger", DecisionLedgerPersistenceDocument.Validate(document));
        return document;
    }

    private static async Task<RoadmapStatePersistenceDocument?> ReadRoadmapStateAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string? json = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT document_json FROM roadmap_state WHERE id = 1;",
            cancellationToken);
        if (json is null)
        {
            return null;
        }

        RoadmapStatePersistenceDocument document = Deserialize<RoadmapStatePersistenceDocument>(json, RoadmapJson.Options);
        ThrowIfInvalid("roadmap state", RoadmapStatePersistenceDocument.Validate(document));
        return document;
    }

    private static async Task<ArtifactLifecyclePersistenceDocument> ReadArtifactLifecycleAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var entries = new List<ArtifactLifecycleEntryDto>();
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            "SELECT path, state, updated_at, notes FROM artifact_lifecycle ORDER BY path COLLATE NOCASE, path;");
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new ArtifactLifecycleEntryDto(
                reader.GetString(0),
                ParseEnum<Primitives.ArtifactStatuses.ArtifactLifecycleState>(reader.GetString(1)),
                ParseDate(reader.GetString(2)),
                reader.GetString(3)));
        }

        var document = new ArtifactLifecyclePersistenceDocument(
            ArtifactLifecyclePersistenceDocument.CurrentSchemaVersion,
            entries);
        ThrowIfInvalid("artifact lifecycle", ArtifactLifecyclePersistenceDocument.Validate(document));
        return document;
    }

    private static async Task<IReadOnlyList<SplitFamilyFilesystemSnapshot>> ReadSplitFamiliesAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<SplitFamilyFilesystemSnapshot>();
        var families = new List<(string FamilyId, string Proposal, string SelectedChild, string Rationale, DateTimeOffset CreatedAt)>();
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            """
            SELECT family_id, proposal, selected_child, selected_child_rationale, created_at
            FROM split_families
            ORDER BY family_id;
            """);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            families.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                ParseDate(reader.GetString(4))));
        }

        foreach ((string familyId, string proposal, string selectedChild, string rationale, DateTimeOffset createdAt) in families)
        {
            IReadOnlyList<string> children = await ReadOrderedPathsAsync(
                connection,
                transaction,
                "split_family_children",
                familyId,
                "child_path",
                cancellationToken);
            IReadOnlyList<string> dependencyOrder = await ReadOrderedPathsAsync(
                connection,
                transaction,
                "split_family_dependency_order",
                familyId,
                "child_path",
                cancellationToken);
            var document = new SplitFamilyPersistenceDocument(
                SplitFamilyPersistenceDocument.CurrentSchemaVersion,
                new SplitFamilyDto(
                    familyId,
                    proposal,
                    children,
                    dependencyOrder,
                    selectedChild,
                    rationale,
                    createdAt));
            ThrowIfInvalid("split lineage", SplitFamilyPersistenceDocument.Validate(document));
            snapshots.Add(new SplitFamilyFilesystemSnapshot(
                familyId,
                RoadmapArtifactPaths.SplitFamilyJson(familyId),
                document));
        }

        return snapshots;
    }

    private static async Task<ExecutionPreparationManifest> ReadExecutionPreparationManifestAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string? json = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT document_json FROM execution_preparation_manifest WHERE id = 1;",
            cancellationToken);
        return json is null ? ExecutionPreparationManifest.Empty : Deserialize<ExecutionPreparationManifest>(json);
    }

    private static async Task<SelectionProvenanceManifest> ReadSelectionProvenanceManifestAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string? json = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT document_json FROM selection_provenance_manifest WHERE id = 1;",
            cancellationToken);
        return json is null ? SelectionProvenanceManifest.Empty : Deserialize<SelectionProvenanceManifest>(json);
    }

    private static async Task<ProjectionManifestPersistenceDocument> ReadProjectionManifestAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var entries = new List<ProjectionManifestEntryDto>();
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            "SELECT document_json FROM projection_manifest_entries ORDER BY runtime_prompt;");
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(Deserialize<ProjectionManifestEntryDto>(reader.GetString(0), RoadmapJson.Options));
        }

        var document = new ProjectionManifestPersistenceDocument(
            ProjectionManifestPersistenceDocument.CurrentSchemaVersion,
            entries);
        ThrowIfInvalid("projection manifest", ProjectionManifestPersistenceDocument.Validate(document));
        return document;
    }

    private static async Task<IReadOnlyList<TransitionJournalRecord>> ReadTransitionJournalAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var records = new List<TransitionJournalRecord>();
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            """
            SELECT event_name, correlation_id, recorded_at, from_state, to_state, transition,
                   projection_path, prompt_contract, input_hashes_json, output_paths_json,
                   duration_milliseconds, result, decision, error, input_snapshot_json
            FROM transition_journal
            ORDER BY event_order;
            """);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string? inputSnapshotJson = reader.IsDBNull(14) ? null : reader.GetString(14);
            records.Add(new TransitionJournalRecord(
                reader.GetString(0),
                reader.GetString(1),
                ParseDate(reader.GetString(2)),
                ParseEnum<RoadmapState>(reader.GetString(3)),
                ParseEnum<RoadmapState>(reader.GetString(4)),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                Deserialize<IReadOnlyDictionary<string, string>>(reader.GetString(8)),
                Deserialize<IReadOnlyList<string>>(reader.GetString(9)),
                reader.GetInt64(10),
                reader.GetString(11),
                reader.GetString(12),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                inputSnapshotJson is null
                    ? null
                    : Deserialize<Models.TransitionInputs.TransitionInputSnapshot>(inputSnapshotJson)));
        }

        return records;
    }

    private static async Task<IReadOnlyList<LoopHistoryFilesystemSnapshot>> ReadLoopHistoriesAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var histories = new List<LoopHistoryFilesystemSnapshot>();
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            "SELECT kind, sequence, logical_path, body, content_hash FROM loop_history ORDER BY kind, sequence;");
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string body = reader.GetString(3);
            string hash = reader.GetString(4);
            if (!string.Equals(hash, Sha256(body), StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Loop history hash mismatch for `{reader.GetString(2)}`.");
            }

            histories.Add(new LoopHistoryFilesystemSnapshot(
                ParseEnum<WorkspaceLoopHistoryKind>(reader.GetString(0)),
                reader.GetInt32(1),
                reader.GetString(2),
                body));
        }

        return histories
            .OrderBy(history => history.Kind)
            .ThenBy(history => history.Sequence)
            .ToArray();
    }

    private static async Task<IReadOnlyList<ExecutionEvidenceFilesystemSnapshot>> ReadExecutionEvidenceAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var evidence = new List<ExecutionEvidenceFilesystemSnapshot>();
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            "SELECT stem, sequence, logical_path, body, content_hash FROM execution_evidence ORDER BY stem, sequence;");
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string body = reader.GetString(3);
            string hash = reader.GetString(4);
            if (!string.Equals(hash, Sha256(body), StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Execution evidence hash mismatch for `{reader.GetString(2)}`.");
            }

            evidence.Add(new ExecutionEvidenceFilesystemSnapshot(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetString(2),
                body));
        }

        return evidence
            .OrderBy(item => item.Stem, StringComparer.Ordinal)
            .ThenBy(item => item.Sequence)
            .ToArray();
    }

    private static async Task InsertOrderedPathsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string familyId,
        IReadOnlyList<string> paths,
        string column,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < paths.Count; i++)
        {
            await ExecuteAsync(
                connection,
                transaction,
                $"INSERT INTO {table} (family_id, ordinal, {column}) VALUES ($family_id, $ordinal, $path);",
                cancellationToken,
                ("$family_id", familyId),
                ("$ordinal", i),
                ("$path", paths[i]));
        }
    }

    private static async Task<IReadOnlyList<string>> ReadOrderedPathsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string table,
        string familyId,
        string column,
        CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        await using SqliteCommand command = CreateCommand(
            connection,
            transaction,
            $"SELECT {column} FROM {table} WHERE family_id = $family_id ORDER BY ordinal;");
        Add(command, "$family_id", familyId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            paths.Add(reader.GetString(0));
        }

        return paths;
    }

    private static async Task<bool> HasDomainRowsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        string[] tables =
        [
            "decision_ledger",
            "roadmap_state",
            "artifact_lifecycle",
            "split_families",
            "execution_preparation_manifest",
            "selection_provenance_manifest",
            "projection_manifest_entries",
            "transition_journal",
            "loop_history",
            "execution_evidence",
        ];

        foreach (string table in tables)
        {
            long count = await ScalarLongAsync(
                connection,
                null,
                $"SELECT COUNT(*) FROM {table};",
                cancellationToken);
            if (count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<int> ReadSchemaVersionAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string? version = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';",
            cancellationToken);
        if (!int.TryParse(version, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            throw new InvalidOperationException("SQLite schema metadata is missing or invalid.");
        }

        return parsed;
    }

    private static Task SetSchemaMetadataAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string key,
        string value,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO schema_metadata (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            cancellationToken,
            ("$key", key),
            ("$value", value));

    private static Task SetWorkspaceMetadataAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string key,
        string value,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO workspace_metadata (key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            cancellationToken,
            ("$key", key),
            ("$value", value));

    private static Task<string?> ReadWorkspaceMetadataAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string key,
        CancellationToken cancellationToken) =>
        ScalarStringAsync(
            connection,
            transaction,
            "SELECT value FROM workspace_metadata WHERE key = $key;",
            cancellationToken,
            ("$key", key));

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = CreateCommand(connection, transaction, commandText);
        foreach ((string name, object? value) in parameters)
        {
            Add(command, name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<string?> ScalarStringAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = CreateCommand(connection, transaction, commandText);
        foreach ((string name, object? value) in parameters)
        {
            Add(command, name, value);
        }

        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<long> ScalarLongAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = CreateCommand(connection, transaction, commandText);
        foreach ((string name, object? value) in parameters)
        {
            Add(command, name, value);
        }

        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string commandText)
    {
        SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        return command;
    }

    private static void Add(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    internal static SqliteConnection OpenReadWriteCreate(string databasePath) =>
        LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);

    internal static SqliteConnection OpenReadOnly(string databasePath) =>
        LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);

    private static string SnapshotJson(WorkspaceFilesystemSnapshot snapshot) =>
        JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);

    private static T Deserialize<T>(string json) =>
        Deserialize<T>(json, SnapshotJsonOptions);

    private static T Deserialize<T>(string json, JsonSerializerOptions options) =>
        JsonSerializer.Deserialize<T>(json, options) ??
        throw new JsonException($"Could not deserialize {typeof(T).Name}.");

    private static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct =>
        Enum.TryParse(value, ignoreCase: false, out TEnum parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid {typeof(TEnum).Name} value `{value}`.");

    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static string Format(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    internal static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static void ThrowIfInvalid(string domain, IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"{domain} is invalid: {string.Join("; ", errors)}");
        }
    }
}
