using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Services.Persistence;

/// <summary>
/// Shared owner for the local `.LoopRelay` SQLite workspace database contract.
/// </summary>
public static class LoopRelayWorkspaceDatabase
{
    public const int CurrentSchemaVersion = 3;
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

        if (existingSchemaVersion == CurrentSchemaVersion)
        {
            return;
        }

        LegacyResumeImport? legacy = await ReadLegacyResumeAsync(connection, cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            int version = existingSchemaVersion ?? 0;
            if (version < 2)
            {
                await ExecuteAsync(connection, transaction, SchemaSql, cancellationToken);
                await StampVersionAsync(connection, transaction, 2, cancellationToken);
                version = 2;
            }

            if (version == 2)
            {
                await ExecuteAsync(connection, transaction, SchemaV3Sql, cancellationToken);
                await ExecuteAsync(
                    connection,
                    transaction,
                    """
                    INSERT INTO workspace_metadata (key, value)
                    SELECT 'persistence_state', 'empty'
                    WHERE NOT EXISTS (SELECT 1 FROM workspace_metadata WHERE key = 'persistence_state');

                    INSERT INTO workspace_metadata (key, value)
                    SELECT 'workspace_id', lower(hex(randomblob(16)))
                    WHERE NOT EXISTS (SELECT 1 FROM workspace_metadata WHERE key = 'workspace_id');
                    """,
                    cancellationToken);
                if (legacy is not null)
                {
                    await ImportLegacyResumeAsync(connection, transaction, legacy, cancellationToken);
                }

                await StampVersionAsync(connection, transaction, 3, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static Task StampVersionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int version,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO schema_metadata (key, value)
            VALUES ('schema_version', $schema_version)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            cancellationToken,
            ("$schema_version", version.ToString(CultureInfo.InvariantCulture)));

    public static async Task<string> ReadWorkspaceIdAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM workspace_metadata WHERE key = 'workspace_id';";
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        string? value = scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
        if (value is null || value.Length != 32 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException("The workspace database has no valid stable workspace_id.");
        }

        return value.ToLowerInvariant();
    }

    public static async Task<string> EnsureSchemaAndReadWorkspaceIdAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(connection, cancellationToken);
        return await ReadWorkspaceIdAsync(connection, cancellationToken);
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

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<LegacyResumeImport?> ReadLegacyResumeAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "decision_session_resume", cancellationToken))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT document_json, saved_at FROM decision_session_resume WHERE id = 1;";
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        string document = reader.GetString(0);
        string savedAt = reader.GetString(1);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document))).ToLowerInvariant();
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(document);
            JsonElement root = parsed.RootElement;
            string? threadId = GetPropertyIgnoreCase(root, "threadId")?.GetString();
            int? schemaVersion = GetPropertyIgnoreCase(root, "schemaVersion") is { } schema && schema.TryGetInt32(out int value)
                ? value
                : null;
            bool valid = schemaVersion == 1 && !string.IsNullOrWhiteSpace(threadId);
            return new LegacyResumeImport(document, savedAt, digest, schemaVersion, threadId, valid,
                valid ? "LegacyScopeUnverified" : "Legacy resume document failed its schema/integrity contract.");
        }
        catch (JsonException exception)
        {
            return new LegacyResumeImport(document, savedAt, digest, null, null, false, exception.GetType().Name);
        }
    }

    private static JsonElement? GetPropertyIgnoreCase(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static async Task ImportLegacyResumeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LegacyResumeImport legacy,
        CancellationToken cancellationToken)
    {
        string importId = $"legacy-sqlite-{legacy.Digest[..16]}";
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT OR IGNORE INTO decision_session_legacy_imports (
                import_id, source_kind, source_digest, document_schema, parse_status,
                provider_thread_id, diagnostic, imported_at
            ) VALUES (
                $import_id, 'LegacySqliteResume', $source_digest, $document_schema, $parse_status,
                $provider_thread_id, $diagnostic, $imported_at
            );
            """,
            cancellationToken,
            ("$import_id", importId),
            ("$source_digest", legacy.Digest),
            ("$document_schema", legacy.SchemaVersion?.ToString(CultureInfo.InvariantCulture)),
            ("$parse_status", legacy.Valid ? "LegacyScopeUnverified" : "QuarantinedCorrupt"),
            ("$provider_thread_id", legacy.ThreadId),
            ("$diagnostic", legacy.Diagnostic),
            ("$imported_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

        if (legacy.Valid)
        {
            string lineageId = $"legacy-{legacy.Digest[..24]}";
            await ExecuteAsync(
                connection,
                transaction,
                """
                INSERT OR IGNORE INTO decision_session_lineage (
                    lineage_id, scope_id, provider, provider_session_id, parent_lineage_id, root_lineage_id,
                    mechanism, completeness, source_digest, profile_digest, plan_digest,
                    created_at, activated_at, retired_at, authority_state
                ) VALUES (
                    $lineage_id, NULL, 'codex', $provider_session_id, NULL, $lineage_id,
                    'LegacyUnscoped', 'Unknown', $source_digest, NULL, NULL,
                    $created_at, NULL, NULL, 'LegacyScopeUnverified'
                );
                """,
                cancellationToken,
                ("$lineage_id", lineageId),
                ("$provider_session_id", legacy.ThreadId),
                ("$source_digest", legacy.Digest),
                ("$created_at", legacy.SavedAt));
        }
    }

    private sealed record LegacyResumeImport(
        string Document,
        string SavedAt,
        string Digest,
        int? SchemaVersion,
        string? ThreadId,
        bool Valid,
        string Diagnostic);

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
            updated_at text not null
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

        CREATE TABLE IF NOT EXISTS canonical_blockers(
            blocker_id text primary key,
            workflow_identity text not null,
            stage_identity text,
            transition_identity text,
            category text not null,
            reason text not null,
            authority text not null,
            required_action text not null,
            recoverable integer not null,
            evidence_json text not null,
            created_at text not null,
            resolved_at text
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
        CREATE INDEX IF NOT EXISTS idx_canonical_blockers_workflow ON canonical_blockers(workflow_identity, stage_identity, transition_identity);
        CREATE INDEX IF NOT EXISTS idx_canonical_workflow_chain_runs_chain ON canonical_workflow_chain_runs(chain_identity, started_at);
        CREATE INDEX IF NOT EXISTS idx_session_telemetry_order
            ON session_telemetry_events(recorded_at, event_id);
        """;

    private const string SchemaV3Sql = """
        CREATE TABLE session_continuity_profiles(
            profile_digest text primary key,
            provider text not null,
            server_version text,
            schema_digest text,
            profile_json text not null,
            evidence_source text not null,
            created_at text not null
        );

        CREATE TABLE decision_session_scopes(
            scope_id text primary key,
            workspace_id text not null,
            workflow_identity text not null,
            prepared_epic_causal_id text not null,
            executable_plan_causal_id text not null,
            session_role text not null,
            contract_version text not null,
            lifecycle_state text not null,
            created_at text not null,
            retired_at text
        );

        CREATE TABLE decision_session_lineage(
            lineage_id text primary key,
            scope_id text,
            provider text not null,
            provider_session_id text not null,
            parent_lineage_id text,
            root_lineage_id text not null,
            mechanism text not null,
            completeness text not null,
            source_digest text,
            profile_digest text,
            plan_digest text,
            created_at text not null,
            activated_at text,
            retired_at text,
            authority_state text not null,
            foreign key(scope_id) references decision_session_scopes(scope_id),
            foreign key(parent_lineage_id) references decision_session_lineage(lineage_id),
            foreign key(profile_digest) references session_continuity_profiles(profile_digest),
            unique(provider, provider_session_id)
        );

        CREATE TABLE decision_session_active(
            scope_id text primary key,
            lineage_id text not null unique,
            occupancy_tokens integer not null,
            reuse_cost real not null,
            reuse_cycles integer not null,
            last_cycle_cost real not null,
            previous_cycle_cost real not null,
            transfer_cost real not null,
            transfer_count integer not null,
            previous_context_size integer,
            context_growth_streak integer not null,
            policy_digest text not null,
            projection_digest text,
            row_version integer not null,
            activated_at text not null,
            foreign key(scope_id) references decision_session_scopes(scope_id),
            foreign key(lineage_id) references decision_session_lineage(lineage_id)
        );

        CREATE TABLE session_recovery_plans(
            plan_digest text primary key,
            plan_id text not null unique,
            schema_version text not null,
            planner_version text not null,
            policy_version text not null,
            mechanism_identity text not null,
            mechanism_version text not null,
            activation_strategy text not null,
            validation_strategy text not null,
            reconciliation_strategy text not null,
            expected_completeness text not null,
            profile_digest text not null,
            canonical_json text not null,
            created_at text not null,
            foreign key(profile_digest) references session_continuity_profiles(profile_digest)
        );

        CREATE TABLE session_recovery_attempts(
            attempt_id text primary key,
            previous_attempt_id text,
            scope_id text not null,
            original_lineage_id text not null,
            replacement_lineage_id text,
            transition_run_id text,
            status text not null,
            row_version integer not null,
            profile_digest text not null,
            plan_digest text,
            failure_classification text,
            failure_json text,
            trigger text not null,
            mechanism_identity text,
            mechanism_version text,
            idempotency_key text not null unique,
            provider_request_id text,
            provider_correlation_id text,
            retry_count integer not null,
            diagnostic_json text,
            created_at text not null,
            updated_at text not null,
            completed_at text,
            foreign key(previous_attempt_id) references session_recovery_attempts(attempt_id),
            foreign key(scope_id) references decision_session_scopes(scope_id),
            foreign key(original_lineage_id) references decision_session_lineage(lineage_id),
            foreign key(replacement_lineage_id) references decision_session_lineage(lineage_id),
            foreign key(profile_digest) references session_continuity_profiles(profile_digest),
            foreign key(plan_digest) references session_recovery_plans(plan_digest)
        );

        CREATE TABLE session_recovery_sources(
            attempt_id text not null,
            source_order integer not null,
            source_kind text not null,
            source_location text not null,
            source_digest text not null,
            verified_boundary text,
            normalizer_version text not null,
            completeness text not null,
            omissions_json text not null,
            descriptor_json text not null,
            primary key(attempt_id, source_order),
            unique(attempt_id, source_kind, source_digest),
            foreign key(attempt_id) references session_recovery_attempts(attempt_id)
        );

        CREATE TABLE decision_session_turns(
            turn_record_id text primary key,
            scope_id text not null,
            lineage_id text not null,
            transition_run_id text not null,
            input_snapshot_hash text not null,
            provider_thread_id text not null,
            provider_turn_id text,
            request_id text,
            state text not null,
            write_started integer not null,
            submitted integer not null,
            accepted integer not null,
            terminal integer not null,
            output_body text,
            output_hash text,
            history_kind text,
            history_sequence integer,
            artifact_materialized integer not null,
            reconciliation_json text,
            row_version integer not null,
            created_at text not null,
            updated_at text not null,
            unique(transition_run_id, input_snapshot_hash),
            foreign key(scope_id) references decision_session_scopes(scope_id),
            foreign key(lineage_id) references decision_session_lineage(lineage_id)
        );

        CREATE TABLE session_transition_correlations(
            transition_run_id text primary key,
            looprelay_session_id text not null,
            lineage_id text not null,
            recovery_attempt_id text,
            provider_thread_id text not null,
            provider_turn_id text,
            turn_record_id text,
            created_at text not null,
            foreign key(lineage_id) references decision_session_lineage(lineage_id),
            foreign key(recovery_attempt_id) references session_recovery_attempts(attempt_id),
            foreign key(turn_record_id) references decision_session_turns(turn_record_id)
        );

        CREATE TABLE decision_session_legacy_imports(
            import_id text primary key,
            source_kind text not null,
            source_digest text not null,
            document_schema text,
            parse_status text not null,
            provider_thread_id text,
            diagnostic text not null,
            imported_at text not null,
            unique(source_kind, source_digest)
        );

        ALTER TABLE loop_history ADD COLUMN producer_run_id text;
        ALTER TABLE loop_history ADD COLUMN producer_lineage_id text;
        ALTER TABLE loop_history ADD COLUMN provider_thread_id text;
        ALTER TABLE loop_history ADD COLUMN provider_turn_id text;
        ALTER TABLE loop_history ADD COLUMN recovery_attempt_id text;

        ALTER TABLE session_telemetry_events ADD COLUMN provider_thread_id text;
        ALTER TABLE session_telemetry_events ADD COLUMN lineage_id text;
        ALTER TABLE session_telemetry_events ADD COLUMN transition_run_id text;
        ALTER TABLE session_telemetry_events ADD COLUMN recovery_attempt_id text;
        ALTER TABLE session_telemetry_events ADD COLUMN continuity_event_type text;
        ALTER TABLE session_telemetry_events ADD COLUMN continuity_outcome text;

        CREATE INDEX idx_decision_scope_lifecycle ON decision_session_scopes(lifecycle_state, scope_id);
        CREATE INDEX idx_decision_lineage_scope_authority ON decision_session_lineage(scope_id, authority_state);
        CREATE INDEX idx_decision_lineage_parent ON decision_session_lineage(parent_lineage_id);
        CREATE INDEX idx_decision_lineage_provider_session ON decision_session_lineage(provider, provider_session_id);
        CREATE INDEX idx_recovery_attempt_scope_status ON session_recovery_attempts(scope_id, status, updated_at);
        CREATE INDEX idx_recovery_attempt_nonterminal ON session_recovery_attempts(status, updated_at);
        CREATE INDEX idx_decision_turn_provider ON decision_session_turns(provider_thread_id, provider_turn_id);
        CREATE INDEX idx_decision_turn_transition ON decision_session_turns(transition_run_id);
        CREATE INDEX idx_session_correlation_provider_turn ON session_transition_correlations(provider_thread_id, provider_turn_id);
        """;
}
