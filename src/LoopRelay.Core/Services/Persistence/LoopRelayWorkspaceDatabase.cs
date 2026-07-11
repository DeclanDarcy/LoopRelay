using System.Globalization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Services.Persistence;

/// <summary>
/// Shared owner for the local `.LoopRelay` SQLite workspace database contract.
/// </summary>
public static class LoopRelayWorkspaceDatabase
{
    public const int CurrentSchemaVersion = 5;
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
        int? existingSchemaVersion = await ReadExistingSchemaVersionAsync(connection, cancellationToken);
        if (existingSchemaVersion is not null &&
            (existingSchemaVersion < 1 || existingSchemaVersion > CurrentSchemaVersion))
        {
            throw new InvalidOperationException(
                $"Unsupported SQLite schema version `{existingSchemaVersion}`; expected `1..{CurrentSchemaVersion}`.");
        }

        await ExecuteAsync(connection, SchemaSql, cancellationToken);
        if (!await ColumnExistsAsync(connection, "canonical_product_records", "schema_version", cancellationToken))
        {
            await ExecuteAsync(
                connection,
                "ALTER TABLE canonical_product_records ADD COLUMN schema_version text not null default '1';",
                cancellationToken);
        }

        await ExecuteAsync(
            connection,
            """
            INSERT INTO schema_metadata (key, value)
            VALUES ('schema_version', $schema_version)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            cancellationToken,
            ("$schema_version", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)));
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
        await ExecuteAsync(
            connection,
            """
            INSERT INTO workspace_identity (id, workspace_id, created_at)
            SELECT 1, $workspace_id, $created_at
            WHERE NOT EXISTS (
                SELECT 1 FROM workspace_identity WHERE id = 1
            );
            """,
            cancellationToken,
            ("$workspace_id", WorkspaceIdentity.New().Value),
            ("$created_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
    }

    public static async Task<string> ReadWorkspaceIdentityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT workspace_id FROM workspace_identity WHERE id = 1;";
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        string? value = scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Workspace identity has not been seeded in the workspace database.");
        }

        return value;
    }

    private static async Task<int?> ReadExistingSchemaVersionAsync(
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
        string? value = scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
        if (value is null)
        {
            return null;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            throw new InvalidOperationException($"Unsupported SQLite schema version `{value}`; expected `1..{CurrentSchemaVersion}`.");
        }

        return parsed;
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

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
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

        CREATE TABLE IF NOT EXISTS canonical_workflow_states(
            workflow_identity text primary key,
            state text not null,
            current_stage text,
            outcome text,
            updated_at text not null,
            evidence_json text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_stage_states(
            workflow_identity text not null,
            stage_identity text not null,
            state text not null,
            updated_at text not null,
            evidence_json text not null,
            primary key(workflow_identity, stage_identity)
        );

        CREATE TABLE IF NOT EXISTS canonical_transition_runs(
            run_id text primary key,
            workflow_identity text not null,
            stage_identity text not null,
            transition_identity text not null,
            state text not null,
            outcome text not null,
            started_at text not null,
            completed_at text,
            input_snapshot_hash text,
            explanation text not null,
            evidence_json text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_transition_evidence(
            evidence_id integer primary key autoincrement,
            run_id text not null,
            transition_identity text not null,
            event_name text not null,
            recorded_at text not null,
            state text not null,
            explanation text not null,
            evidence_json text not null,
            document_json text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_product_records(
            product_identity text primary key,
            producer_workflow text not null,
            producer_transition text not null,
            intended_consumers_json text not null,
            repository_ownership text not null,
            authority text not null,
            storage_representations_json text not null,
            causal_identity text not null,
            freshness text not null,
            validation_state text not null,
            lifecycle text not null,
            evidence_locations_json text not null,
            updated_at text not null,
            schema_version text not null default '1'
        );

        CREATE TABLE IF NOT EXISTS canonical_gate_evaluations(
            evaluation_id integer primary key autoincrement,
            workflow_identity text not null,
            stage_identity text,
            transition_identity text,
            gate_identity text not null,
            status text not null,
            evaluated_at text not null,
            requirements_json text not null,
            explanation text not null,
            evidence_json text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_effect_records(
            record_id integer primary key autoincrement,
            run_id text not null,
            effect_identity text not null,
            category text not null,
            status text not null,
            recorded_at text not null,
            explanation text not null,
            evidence_json text not null
        );

        drop table if exists canonical_blockers;

        CREATE TABLE IF NOT EXISTS evaluation_warnings(
            warning_id text primary key,
            workflow_identity text not null,
            stage_identity text,
            transition_identity text,
            category text not null,
            concern text not null,
            authority text not null,
            remediation text not null,
            evidence_json text not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_recovery_markers(
            marker_id text primary key,
            workflow_identity text not null,
            stage_identity text,
            transition_identity text,
            semantics text not null,
            supported_actions_json text not null,
            unsupported_actions_json text not null,
            evidence_json text not null,
            recorded_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_workflow_chain_runs(
            chain_run_id text primary key,
            chain_identity text not null,
            current_workflow text not null,
            status text not null,
            started_at text not null,
            completed_at text,
            explanation text not null,
            evidence_json text not null
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
        CREATE INDEX IF NOT EXISTS idx_canonical_stage_states_workflow ON canonical_stage_states(workflow_identity);
        CREATE INDEX IF NOT EXISTS idx_canonical_transition_runs_workflow_stage
            ON canonical_transition_runs(workflow_identity, stage_identity, transition_identity);
        CREATE INDEX IF NOT EXISTS idx_canonical_transition_evidence_run ON canonical_transition_evidence(run_id);
        CREATE INDEX IF NOT EXISTS idx_canonical_gate_evaluations_workflow
            ON canonical_gate_evaluations(workflow_identity, stage_identity, transition_identity);
        CREATE INDEX IF NOT EXISTS idx_canonical_effect_records_run ON canonical_effect_records(run_id);
        CREATE INDEX IF NOT EXISTS idx_evaluation_warnings_workflow ON evaluation_warnings(workflow_identity, stage_identity, transition_identity);
        CREATE INDEX IF NOT EXISTS idx_canonical_workflow_chain_runs_chain ON canonical_workflow_chain_runs(chain_identity, started_at);
        CREATE INDEX IF NOT EXISTS idx_session_telemetry_order
            ON session_telemetry_events(recorded_at, event_id);

        CREATE TABLE IF NOT EXISTS workspace_identity(
            id integer primary key check (id = 1),
            workspace_id text not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS runs(
            run_id text primary key,
            workspace_id text not null,
            chain_identity text not null,
            invocation_mode text not null,
            status text not null,
            started_at text not null,
            completed_at text,
            stop_reason text,
            explanation text not null
        );

        CREATE TABLE IF NOT EXISTS workflow_instances(
            workflow_instance_id text primary key,
            run_id text not null,
            workflow_identity text not null,
            catalog_version text not null,
            status text not null,
            started_at text not null,
            completed_at text,
            outcome text
        );

        CREATE TABLE IF NOT EXISTS attempts(
            attempt_id text primary key,
            transition_run_id text not null,
            workflow_instance_id text not null,
            run_id text not null,
            attempt_index integer not null,
            started_at text not null,
            completed_at text,
            outcome text,
            unique(transition_run_id, attempt_index)
        );

        CREATE TABLE IF NOT EXISTS agent_sessions(
            session_id text primary key,
            attempt_id text,
            workspace_id text,
            provider text not null,
            provider_thread_id text,
            role text not null,
            legacy_session_guid text,
            started_at text not null,
            completed_at text
        );

        CREATE TABLE IF NOT EXISTS agent_turns(
            turn_id text primary key,
            session_id text not null,
            turn_index integer not null,
            recorded_at text not null,
            unique(session_id, turn_index)
        );

        CREATE INDEX IF NOT EXISTS idx_runs_workspace_started ON runs(workspace_id, started_at);
        CREATE INDEX IF NOT EXISTS idx_workflow_instances_run ON workflow_instances(run_id);
        CREATE INDEX IF NOT EXISTS idx_workflow_instances_workflow ON workflow_instances(workflow_identity, started_at);
        CREATE INDEX IF NOT EXISTS idx_attempts_transition_run ON attempts(transition_run_id);
        CREATE INDEX IF NOT EXISTS idx_agent_sessions_attempt ON agent_sessions(attempt_id);

        CREATE TABLE IF NOT EXISTS read_receipts(
            receipt_id text primary key,
            run_id text not null,
            workflow_identity text not null,
            transition_identity text not null,
            attempt_id text,
            commit_hash text,
            input_surfaces_json text not null,
            surface_tree_hashes_json text,
            files_json text not null,
            products_json text not null,
            validation text not null,
            consumed_at text not null
        );

        UPDATE canonical_workflow_states SET state = 'Resumable' WHERE state = 'Blocked';
        UPDATE canonical_workflow_states SET outcome = 'MissingRequiredInput' WHERE outcome = 'Blocked';
        UPDATE canonical_stage_states SET state = 'Resumable' WHERE state = 'Blocked';
        UPDATE canonical_transition_runs SET state = 'InputUnsatisfied' WHERE state = 'Blocked';
        UPDATE canonical_transition_runs SET outcome = 'MissingRequiredInput' WHERE outcome = 'Blocked';
        """;
}
