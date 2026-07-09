using System.Globalization;
using LoopRelay.Core.Models.Repositories;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Services.Persistence;

/// <summary>
/// Shared owner for the local `.LoopRelay` SQLite workspace database contract.
/// </summary>
public static class LoopRelayWorkspaceDatabase
{
    public const int CurrentSchemaVersion = 1;
    public const string RelativeDatabasePath = ".LoopRelay/persistence/looprelay.sqlite3";

    public static string Resolve(Repository repository)
    {
        string workspaceRoot = Path.GetFullPath(repository.Path);
        string databasePath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(workspaceRoot, databasePath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("Resolved workspace database path escaped the repository root.");
        }

        return databasePath;
    }

    public static async Task EnsureSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
        string? existingSchemaVersion = await ReadExistingSchemaVersionAsync(connection, cancellationToken);
        string currentSchemaVersion = CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture);
        if (existingSchemaVersion is not null &&
            !string.Equals(existingSchemaVersion, currentSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported SQLite schema version `{existingSchemaVersion}`; expected `{currentSchemaVersion}`.");
        }

        await ExecuteAsync(connection, SchemaSql, cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO schema_metadata (key, value)
            VALUES ('schema_version', $schema_version)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            cancellationToken,
            ("$schema_version", currentSchemaVersion));
        await ExecuteAsync(
            connection,
            """
            INSERT INTO workspace_metadata (key, value)
            SELECT 'persistence_state', 'empty'
            WHERE NOT EXISTS (
                SELECT 1 FROM workspace_metadata WHERE key = 'persistence_state'
            );
            """,
            cancellationToken);
    }

    private static async Task<string?> ReadExistingSchemaVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "schema_metadata", cancellationToken))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_metadata WHERE key = 'schema_version';";
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
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

    public static SqliteConnection OpenReadWriteCreate(string databasePath) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString());

    public static SqliteConnection OpenReadWrite(string databasePath) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());

    public static SqliteConnection OpenReadOnly(string databasePath) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS schema_metadata(
            key text primary key,
            value text not null
        );

        CREATE TABLE IF NOT EXISTS workspace_metadata(
            key text primary key,
            value text not null
        );

        CREATE TABLE IF NOT EXISTS sync_markers(
            domain text primary key,
            canonical_hash text not null,
            export_hash text,
            generation integer not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS decision_ledger(
            decision_id text primary key,
            timestamp text not null,
            state text not null,
            transition text not null,
            prompt text not null,
            projection_path text not null,
            input_paths_json text not null,
            output_paths_json text not null,
            decision text not null,
            confidence text not null,
            rationale_excerpt text not null
        );

        CREATE TABLE IF NOT EXISTS roadmap_state(
            id integer primary key check (id = 1),
            document_json text not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS artifact_lifecycle(
            path_key text primary key,
            path text not null,
            state text not null,
            updated_at text not null,
            notes text not null
        );

        CREATE TABLE IF NOT EXISTS split_families(
            family_id text primary key,
            proposal text not null,
            selected_child text not null,
            selected_child_rationale text not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS split_family_children(
            family_id text not null,
            ordinal integer not null,
            child_path text not null,
            primary key(family_id, ordinal),
            unique(family_id, child_path)
        );

        CREATE TABLE IF NOT EXISTS split_family_dependency_order(
            family_id text not null,
            ordinal integer not null,
            child_path text not null,
            primary key(family_id, ordinal)
        );

        CREATE TABLE IF NOT EXISTS execution_preparation_manifest(
            id integer primary key check (id = 1),
            document_json text not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS selection_provenance_manifest(
            id integer primary key check (id = 1),
            document_json text not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS projection_manifest_entries(
            runtime_prompt text primary key,
            document_json text not null,
            updated_at text not null
        );

        CREATE TABLE IF NOT EXISTS transition_journal(
            event_order integer primary key autoincrement,
            correlation_id text not null,
            event_name text not null,
            recorded_at text not null,
            from_state text not null,
            to_state text not null,
            transition text not null,
            projection_path text not null,
            prompt_contract text not null,
            input_hashes_json text not null,
            output_paths_json text not null,
            duration_milliseconds integer not null,
            retry_count integer not null,
            result text not null,
            decision text not null,
            error text,
            input_snapshot_json text
        );

        CREATE TABLE IF NOT EXISTS loop_history(
            kind text not null,
            sequence integer not null,
            logical_path text not null unique,
            body text not null,
            content_hash text not null,
            created_at text not null,
            primary key(kind, sequence)
        );

        CREATE TABLE IF NOT EXISTS execution_evidence(
            logical_path text primary key,
            stem text not null,
            sequence integer not null,
            body text not null,
            content_hash text not null,
            created_at text not null,
            writer text,
            metadata_json text not null,
            unique(stem, sequence)
        );

        CREATE TABLE IF NOT EXISTS completed_epic_archives(
            archive_index integer primary key,
            archive_directory text not null unique,
            synthesis_path text not null unique,
            created_at text not null,
            metadata_json text not null
        );

        CREATE TABLE IF NOT EXISTS completed_epic_records(
            archive_index integer not null,
            domain text not null,
            logical_path text not null,
            export_path text not null,
            content_hash text not null,
            primary key(archive_index, domain, logical_path)
        );

        CREATE TABLE IF NOT EXISTS workflow_transactions(
            transaction_id text primary key,
            workflow_name text not null,
            correlation_id text not null,
            status text not null,
            started_at text not null,
            completed_at text,
            marker_json text not null
        );

        CREATE TABLE IF NOT EXISTS decision_session_resume(
            id integer primary key check (id = 1),
            document_json text not null,
            saved_at text not null
        );

        CREATE TABLE IF NOT EXISTS session_telemetry_events(
            event_id integer primary key autoincrement,
            recorded_at text not null,
            repo_name text not null,
            session_id text not null,
            session_type text not null,
            turn_index integer not null,
            document_json text not null,
            content_hash text not null
        );

        CREATE INDEX IF NOT EXISTS idx_artifact_lifecycle_path_key ON artifact_lifecycle(path_key);
        CREATE INDEX IF NOT EXISTS idx_split_family_children_child_path ON split_family_children(child_path);
        CREATE INDEX IF NOT EXISTS idx_transition_journal_correlation_id ON transition_journal(correlation_id);
        CREATE INDEX IF NOT EXISTS idx_loop_history_kind_sequence_desc ON loop_history(kind, sequence desc);
        CREATE INDEX IF NOT EXISTS idx_execution_evidence_stem_sequence_desc ON execution_evidence(stem, sequence desc);
        CREATE INDEX IF NOT EXISTS idx_session_telemetry_order
            ON session_telemetry_events(recorded_at, event_id);
        """;
}
