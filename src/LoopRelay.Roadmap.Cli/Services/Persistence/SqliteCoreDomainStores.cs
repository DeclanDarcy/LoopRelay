using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Roadmap.Cli.Abstractions.Persistence;
using LoopRelay.Roadmap.Cli.Models.ArtifactRecords;
using LoopRelay.Roadmap.Cli.Models.Decisions;
using LoopRelay.Roadmap.Cli.Models.Execution;
using LoopRelay.Roadmap.Cli.Models.RoadmapState;
using LoopRelay.Roadmap.Cli.Models.Splits;
using LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;
using LoopRelay.Roadmap.Cli.Primitives.State;
using LoopRelay.Roadmap.Cli.Services.Artifacts;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Roadmap.Cli.Services.Persistence;

internal abstract class SqliteDomainStore(Repository repository)
{
    protected Repository Repository { get; } = repository;

    protected async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        string databasePath = WorkspaceDatabaseLocator.Resolve(Repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        SqliteConnection connection = WorkspaceSqliteStore.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await WorkspaceSqliteStore.EnsureSchemaAsync(connection, cancellationToken);
        return connection;
    }

    protected static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        CancellationToken cancellationToken = default,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    protected static async Task<string?> ScalarStringAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken = default,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    protected static async Task<long> ScalarLongAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken = default,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    protected static T Deserialize<T>(string json, JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(json, options ?? JsonOptions) ??
        throw new RoadmapStepException($"SQLite row could not be deserialized as {typeof(T).Name}.");

    protected static string Format(DateTimeOffset value) =>
        value.ToString("O", CultureInfo.InvariantCulture);

    protected static TEnum ParseEnum<TEnum>(string value)
        where TEnum : struct =>
        Enum.TryParse(value, out TEnum parsed)
            ? parsed
            : throw new RoadmapStepException($"SQLite row contains invalid {typeof(TEnum).Name} value `{value}`.");

    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

internal sealed class SqliteDecisionLedgerStore(Repository repository) : SqliteDomainStore(repository), IDecisionLedgerStore
{
    public async Task<string> AppendAsync(DecisionLedgerEntry entry)
    {
        await using SqliteConnection connection = await OpenAsync();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();
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
            parameters:
            [
                ("$decision_id", entry.DecisionId),
                ("$timestamp", Format(entry.Timestamp)),
                ("$state", entry.State.ToString()),
                ("$transition", entry.Transition),
                ("$prompt", entry.Prompt),
                ("$projection_path", entry.ProjectionPath),
                ("$input_paths_json", JsonSerializer.Serialize(entry.InputArtifactPaths, JsonOptions)),
                ("$output_paths_json", JsonSerializer.Serialize(entry.OutputArtifactPaths, JsonOptions)),
                ("$decision", entry.Decision),
                ("$confidence", entry.Confidence),
                ("$rationale_excerpt", entry.RationaleExcerpt),
            ]);
        await transaction.CommitAsync();
        return entry.DecisionId;
    }

    public async Task<string> NextDecisionIdAsync()
    {
        await using SqliteConnection connection = await OpenAsync();
        int max = await MaxDecisionNumberAsync(connection);
        return $"D{max + 1:0000}";
    }

    public async Task<string> LastDecisionIdAsync()
    {
        await using SqliteConnection connection = await OpenAsync();
        int max = await MaxDecisionNumberAsync(connection);
        return max == 0 ? "None" : $"D{max:0000}";
    }

    private static async Task<int> MaxDecisionNumberAsync(SqliteConnection connection)
    {
        string? max = await ScalarStringAsync(
            connection,
            "SELECT MAX(CAST(SUBSTR(decision_id, 2) AS INTEGER)) FROM decision_ledger WHERE decision_id GLOB 'D[0-9][0-9][0-9][0-9]';");
        return int.TryParse(max, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
    }
}

internal sealed class SqliteRoadmapStateStore(Repository repository) : SqliteDomainStore(repository), IRoadmapStateStore
{
    public async Task SaveAsync(RoadmapStateDocument document)
    {
        RoadmapStatePersistenceDocument persisted = RoadmapStatePersistenceDocument.FromDomain(document);
        IReadOnlyList<string> errors = RoadmapStatePersistenceDocument.Validate(persisted);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"Roadmap state cannot be saved to SQLite: {string.Join("; ", errors)}");
        }

        await using SqliteConnection connection = await OpenAsync();
        await ExecuteAsync(
            connection,
            null,
            """
            INSERT INTO roadmap_state (id, document_json, updated_at)
            VALUES (1, $document_json, $updated_at)
            ON CONFLICT(id) DO UPDATE SET
                document_json = excluded.document_json,
                updated_at = excluded.updated_at;
            """,
            parameters:
            [
                ("$document_json", JsonSerializer.Serialize(persisted, RoadmapJson.Options)),
                ("$updated_at", Format(DateTimeOffset.UtcNow)),
            ]);
    }

    public async Task<RoadmapStateDocument?> LoadAsync()
    {
        await using SqliteConnection connection = await OpenAsync();
        string? json = await ScalarStringAsync(connection, "SELECT document_json FROM roadmap_state WHERE id = 1;");
        if (json is null)
        {
            return null;
        }

        RoadmapStatePersistenceDocument persisted = Deserialize<RoadmapStatePersistenceDocument>(json, RoadmapJson.Options);
        IReadOnlyList<string> errors = RoadmapStatePersistenceDocument.Validate(persisted);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"SQLite roadmap state is invalid: {string.Join("; ", errors)}");
        }

        return persisted.ToDomain();
    }
}

internal sealed class SqliteArtifactLifecycleStore(Repository repository) : SqliteDomainStore(repository), IArtifactLifecycleStore
{
    public async Task<IReadOnlyList<ArtifactLifecycleEntry>> LoadAsync()
    {
        var entries = new List<ArtifactLifecycleEntry>();
        await using SqliteConnection connection = await OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT path, state, updated_at, notes FROM artifact_lifecycle ORDER BY path COLLATE NOCASE, path;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new ArtifactLifecycleEntry(
                reader.GetString(0),
                ParseEnum<ArtifactLifecycleState>(reader.GetString(1)),
                DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.GetString(3)));
        }

        return entries;
    }

    public async Task UpsertAsync(string path, ArtifactLifecycleState state, string notes = "")
    {
        await using SqliteConnection connection = await OpenAsync();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();
        await ExecuteAsync(
            connection,
            transaction,
            "DELETE FROM artifact_lifecycle WHERE path_key = $path_key;",
            parameters: [("$path_key", PathKey(path))]);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO artifact_lifecycle (path_key, path, state, updated_at, notes)
            VALUES ($path_key, $path, $state, $updated_at, $notes);
            """,
            parameters:
            [
                ("$path_key", PathKey(path)),
                ("$path", path),
                ("$state", state.ToString()),
                ("$updated_at", Format(DateTimeOffset.UtcNow)),
                ("$notes", notes),
            ]);
        await transaction.CommitAsync();
    }

    public async Task SaveAsync(IReadOnlyList<ArtifactLifecycleEntry> entries)
    {
        ArtifactLifecyclePersistenceDocument persisted = ArtifactLifecyclePersistenceDocument.FromDomain(entries);
        IReadOnlyList<string> errors = ArtifactLifecyclePersistenceDocument.Validate(persisted);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"Artifact lifecycle cannot be saved to SQLite: {string.Join("; ", errors)}");
        }

        await using SqliteConnection connection = await OpenAsync();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "DELETE FROM artifact_lifecycle;");
        foreach (ArtifactLifecycleEntryDto entry in persisted.Entries)
        {
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT INTO artifact_lifecycle (path_key, path, state, updated_at, notes)
                VALUES ($path_key, $path, $state, $updated_at, $notes);
                """,
                parameters:
                [
                    ("$path_key", PathKey(entry.Path)),
                    ("$path", entry.Path),
                    ("$state", entry.State.ToString()),
                    ("$updated_at", Format(entry.UpdatedAt)),
                    ("$notes", entry.Notes),
                ]);
        }

        await transaction.CommitAsync();
    }

    private static string PathKey(string path) => path.ToUpperInvariant();
}

internal sealed class SqliteSplitFamilyStore(Repository repository) : SqliteDomainStore(repository), ISplitFamilyStore
{
    public async Task<string> WriteAsync(SplitFamily family)
    {
        SplitFamilyPersistenceDocument persisted = SplitFamilyPersistenceDocument.FromDomain(family);
        IReadOnlyList<string> errors = SplitFamilyPersistenceDocument.Validate(persisted);
        if (errors.Count > 0)
        {
            throw new RoadmapStepException($"Split family cannot be saved to SQLite: {string.Join("; ", errors)}");
        }

        await using SqliteConnection connection = await OpenAsync();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();
        await ExecuteAsync(connection, transaction, "DELETE FROM split_family_children WHERE family_id = $family_id;", parameters: [("$family_id", family.FamilyId)]);
        await ExecuteAsync(connection, transaction, "DELETE FROM split_family_dependency_order WHERE family_id = $family_id;", parameters: [("$family_id", family.FamilyId)]);
        await ExecuteAsync(connection, transaction, "DELETE FROM split_families WHERE family_id = $family_id;", parameters: [("$family_id", family.FamilyId)]);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO split_families (family_id, proposal, selected_child, selected_child_rationale, created_at)
            VALUES ($family_id, $proposal, $selected_child, $selected_child_rationale, $created_at);
            """,
            parameters:
            [
                ("$family_id", family.FamilyId),
                ("$proposal", family.Proposal),
                ("$selected_child", family.SelectedChildPath),
                ("$selected_child_rationale", family.SelectedChildRationale),
                ("$created_at", Format(family.CreatedAt)),
            ]);
        await InsertPathsAsync(connection, transaction, "split_family_children", family.FamilyId, family.ChildEpicPaths);
        await InsertPathsAsync(connection, transaction, "split_family_dependency_order", family.FamilyId, family.DependencyOrder);
        await transaction.CommitAsync();
        return RoadmapArtifactPaths.SplitFamilyJson(family.FamilyId);
    }

    public async Task<bool> ExistsForChildAsync(string childEpicPath)
    {
        await using SqliteConnection connection = await OpenAsync();
        long count = await ScalarLongAsync(
            connection,
            "SELECT COUNT(*) FROM split_family_children WHERE child_path = $child_path;",
            parameters: [("$child_path", childEpicPath)]);
        return count > 0;
    }

    public async Task<int> CountAsync()
    {
        await using SqliteConnection connection = await OpenAsync();
        return (int)await ScalarLongAsync(connection, "SELECT COUNT(*) FROM split_families;");
    }

    private static async Task InsertPathsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string familyId,
        IReadOnlyList<string> paths)
    {
        for (int i = 0; i < paths.Count; i++)
        {
            await ExecuteAsync(
                connection,
                transaction,
                $"INSERT INTO {table} (family_id, ordinal, child_path) VALUES ($family_id, $ordinal, $child_path);",
                parameters:
                [
                    ("$family_id", familyId),
                    ("$ordinal", i),
                    ("$child_path", paths[i]),
                ]);
        }
    }
}
