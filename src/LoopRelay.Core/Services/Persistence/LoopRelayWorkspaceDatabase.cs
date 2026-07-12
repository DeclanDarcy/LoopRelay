using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Services.Persistence;

public enum WorkspaceSchemaFamily
{
    Empty,
    CanonicalWorkspace,
    LegacyContinuity,
    Unknown,
}

public sealed record WorkspaceSchemaInspection(
    string? SchemaIdentity,
    WorkspaceSchemaFamily Family,
    int? Version,
    bool HasExplicitLineage,
    string Diagnostic);

public sealed class WorkspaceCompatibilityImportRequiredException(
    WorkspaceSchemaInspection inspection)
    : InvalidOperationException(
        $"Workspace database requires explicit compatibility import: {inspection.Diagnostic}")
{
    public WorkspaceSchemaInspection Inspection { get; } = inspection;
}

/// <summary>
/// Shared owner for the local `.LoopRelay` SQLite workspace database contract.
/// </summary>
public static class LoopRelayWorkspaceDatabase
{
    public const string SchemaIdentity = "looprelay.workspace-state";
    public const string SchemaFamily = "CanonicalWorkspace";
    public const int CurrentSchemaVersion = 9;
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
        WorkspaceSchemaInspection inspection = await InspectSchemaAsync(connection, cancellationToken);
        if (inspection.Family == WorkspaceSchemaFamily.LegacyContinuity)
        {
            throw new WorkspaceCompatibilityImportRequiredException(inspection);
        }

        if (inspection.Family == WorkspaceSchemaFamily.Unknown)
        {
            throw new InvalidOperationException($"Unsupported workspace schema: {inspection.Diagnostic}");
        }

        if (inspection.Version is > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported SQLite schema version `{inspection.Version}`; expected `1..{CurrentSchemaVersion}`.");
        }

        LegacyResumeImport? legacyResume = await ReadLegacyResumeAsync(connection, cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await EnsureCanonicalV8ShapeAsync(connection, transaction, cancellationToken);
            await ExecuteAsync(connection, transaction, SchemaV9Sql, cancellationToken);
            await EnsureV9ColumnsAsync(connection, transaction, cancellationToken);
            if (legacyResume is not null)
            {
                await ImportLegacyResumeAsync(connection, transaction, legacyResume, cancellationToken);
            }
            string workspaceId = await EnsureImmutableWorkspaceIdentityAsync(
                connection,
                transaction,
                inspection,
                cancellationToken);
            await StampCanonicalSchemaAsync(connection, transaction, inspection, workspaceId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public static async Task<WorkspaceSchemaInspection> InspectSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        if (!await TableExistsAsync(connection, "schema_metadata", cancellationToken))
        {
            long tableCount = await CountUserTablesAsync(connection, cancellationToken);
            return tableCount == 0
                ? new WorkspaceSchemaInspection(null, WorkspaceSchemaFamily.Empty, null, false, "Empty database.")
                : new WorkspaceSchemaInspection(null, WorkspaceSchemaFamily.Unknown, null, false,
                    "Database contains tables but no schema metadata.");
        }

        string? identity = await ReadMetadataValueAsync(connection, "schema_identity", cancellationToken);
        string? family = await ReadMetadataValueAsync(connection, "schema_family", cancellationToken);
        int? version = await ReadExistingSchemaVersionAsync(connection, cancellationToken);
        if (identity is not null || family is not null)
        {
            bool canonical = string.Equals(identity, SchemaIdentity, StringComparison.Ordinal) &&
                string.Equals(family, SchemaFamily, StringComparison.Ordinal);
            return new WorkspaceSchemaInspection(
                identity,
                canonical ? WorkspaceSchemaFamily.CanonicalWorkspace : WorkspaceSchemaFamily.Unknown,
                version,
                true,
                canonical
                    ? $"Canonical workspace schema v{version}."
                    : $"Unrecognized schema lineage identity='{identity}', family='{family}'.");
        }

        bool legacyContinuity = version == 3 &&
            await TableExistsAsync(connection, "session_continuity_profiles", cancellationToken) &&
            await TableExistsAsync(connection, "decision_session_scopes", cancellationToken) &&
            !await TableExistsAsync(connection, "workspace_identity", cancellationToken);
        if (legacyContinuity)
        {
            return new WorkspaceSchemaInspection(
                "looprelay.legacy-continuity",
                WorkspaceSchemaFamily.LegacyContinuity,
                version,
                false,
                "Detected branch-local LegacyContinuity v3 by structural fingerprint.");
        }

        // Numeric pre-lineage versions 1..8 belong to the historical canonical family unless
        // the v3 continuity fingerprint above proves otherwise. Older test/production databases
        // may contain only the tables introduced by their version, so table completeness cannot
        // be required before the migration itself reconstructs the canonical shape.
        bool recognizableCanonical = version is >= 1 and <= 8;
        return recognizableCanonical
            ? new WorkspaceSchemaInspection(
                SchemaIdentity,
                WorkspaceSchemaFamily.CanonicalWorkspace,
                version,
                false,
                $"Recognized pre-lineage canonical workspace schema v{version}.")
            : new WorkspaceSchemaInspection(
                null,
                WorkspaceSchemaFamily.Unknown,
                version,
                false,
                $"Schema version {version?.ToString(CultureInfo.InvariantCulture) ?? "(missing)"} has an unknown structural fingerprint.");
    }

    private static async Task EnsureCanonicalV8ShapeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction, SchemaSql, cancellationToken);
        await AddColumnIfMissingAsync(
            connection,
            transaction,
            "canonical_product_records",
            "schema_version",
            "text not null default '1'",
            cancellationToken);
        foreach ((string table, string column) in V6LineageColumns)
        {
            await AddColumnIfMissingAsync(
                connection, transaction, table, column, "text", cancellationToken);
        }

        await AddColumnIfMissingAsync(
            connection, transaction, "attempts", "policy_id", "text", cancellationToken);
        foreach (string column in (string[])["effort", "sandbox"])
        {
            await AddColumnIfMissingAsync(
                connection, transaction, "agent_sessions", column, "text", cancellationToken);
        }
    }

    private static async Task EnsureV9ColumnsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach ((string table, string column) in V9CausalColumns)
        {
            await AddColumnIfMissingAsync(
                connection, transaction, table, column, "text", cancellationToken);
        }
    }

    private static async Task<string> EnsureImmutableWorkspaceIdentityAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        WorkspaceSchemaInspection inspection,
        CancellationToken cancellationToken)
    {
        string? existing = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT workspace_id FROM workspace_identity WHERE id = 1;",
            cancellationToken);
        string? legacy = await ScalarStringAsync(
            connection,
            transaction,
            "SELECT value FROM workspace_metadata WHERE key = 'workspace_id';",
            cancellationToken);
        string workspaceId = existing ?? legacy ?? WorkspaceIdentity.New().Value;
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            throw new InvalidOperationException("Workspace identity must not be empty.");
        }

        if (existing is not null && legacy is not null && !string.Equals(existing, legacy, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Workspace identity is immutable, but canonical and legacy identity records disagree.");
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO workspace_identity (id, workspace_id, created_at)
            VALUES (1, $workspace_id, $created_at)
            ON CONFLICT(id) DO NOTHING;
            """,
            cancellationToken,
            ("$workspace_id", workspaceId),
            ("$created_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        string identityFormat = workspaceId.StartsWith("ws_", StringComparison.Ordinal)
            ? "prefixed-ulid"
            : "legacy-opaque";
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO workspace_identity_metadata (
                id, identity_format, migration_source, imported_at
            )
            VALUES (1, $identity_format, $migration_source, $imported_at)
            ON CONFLICT(id) DO NOTHING;
            """,
            cancellationToken,
            ("$identity_format", identityFormat),
            ("$migration_source", inspection.Family == WorkspaceSchemaFamily.Empty
                ? "fresh-v9"
                : $"{inspection.SchemaIdentity ?? SchemaIdentity}:v{inspection.Version}"),
            ("$imported_at", inspection.Family == WorkspaceSchemaFamily.Empty
                ? null
                : DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        return workspaceId;
    }

    private static async Task StampCanonicalSchemaAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        WorkspaceSchemaInspection inspection,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        foreach ((string key, string value) in (KeyValuePair<string, string>[])
        [
            new("schema_identity", SchemaIdentity),
            new("schema_family", SchemaFamily),
            new("schema_version", CurrentSchemaVersion.ToString(CultureInfo.InvariantCulture)),
        ])
        {
            await ExecuteAsync(
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
        }

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO workspace_metadata (key, value)
            VALUES ('persistence_state', 'empty')
            ON CONFLICT(key) DO NOTHING;
            """,
            cancellationToken);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO workspace_schema_migrations (
                migration_id, schema_identity, schema_family, from_version, to_version,
                workspace_id, applied_at
            )
            VALUES (
                $migration_id, $schema_identity, $schema_family, $from_version, $to_version,
                $workspace_id, $applied_at
            )
            ON CONFLICT(migration_id) DO NOTHING;
            """,
            cancellationToken,
            ("$migration_id", $"{SchemaIdentity}:{inspection.Version ?? 0}->{CurrentSchemaVersion}:{workspaceId}"),
            ("$schema_identity", SchemaIdentity),
            ("$schema_family", SchemaFamily),
            ("$from_version", inspection.Version ?? 0),
            ("$to_version", CurrentSchemaVersion),
            ("$workspace_id", workspaceId),
            ("$applied_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
    }

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string column,
        string declaration,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, table, column, cancellationToken, transaction))
        {
            await ExecuteAsync(
                connection,
                transaction,
                $"ALTER TABLE {table} ADD COLUMN {column} {declaration};",
                cancellationToken);
        }
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

    public static async Task<string> EnsureSchemaAndReadWorkspaceIdAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(connection, cancellationToken);
        return await ReadWorkspaceIdentityAsync(connection, cancellationToken);
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

    private static async Task<string?> ReadMetadataValueAsync(
        SqliteConnection connection,
        string key,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM schema_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<long> CountUserTablesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture) == 1;
    }

    private static readonly (string Table, string Column)[] V6LineageColumns =
    [
        ("loop_history", "history_id"),
        ("loop_history", "run_id"),
        ("loop_history", "transition_run_id"),
        ("loop_history", "attempt_id"),
        ("read_receipts", "transition_run_id"),
        ("evaluation_warnings", "transition_run_id"),
        ("canonical_gate_evaluations", "transition_run_id"),
    ];

    private static readonly (string Table, string Column)[] V9CausalColumns =
    [
        ("loop_history", "workspace_id"),
        ("loop_history", "workflow_instance_id"),
        ("loop_history", "session_id"),
        ("loop_history", "turn_id"),
        ("loop_history", "supersedes_id"),
        ("loop_history", "producer_run_id"),
        ("loop_history", "producer_lineage_id"),
        ("loop_history", "provider_thread_id"),
        ("loop_history", "provider_turn_id"),
        ("loop_history", "recovery_attempt_id"),
        ("canonical_rendered_prompts", "persistence_id"),
        ("canonical_rendered_prompts", "prompt_policy_profile_id"),
        ("canonical_rendered_prompts", "consumed_input_manifest_id"),
        ("canonical_rendered_prompts", "rendered_encoding"),
        ("prompt_dispatch_events", "session_id"),
        ("prompt_dispatch_events", "turn_id"),
        ("runtime_profile_evaluations", "provider_capability_json"),
        ("session_telemetry_events", "provider_thread_id"),
        ("session_telemetry_events", "lineage_id"),
        ("session_telemetry_events", "transition_run_id"),
        ("session_telemetry_events", "recovery_attempt_id"),
        ("session_telemetry_events", "continuity_event_type"),
        ("session_telemetry_events", "continuity_outcome"),
    ];

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM pragma_table_info($table) WHERE name = $column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<string?> ScalarStringAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
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
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(document)));
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(document);
            JsonElement root = parsed.RootElement;
            string? threadId = GetPropertyIgnoreCase(root, "threadId")?.GetString();
            int? schemaVersion = GetPropertyIgnoreCase(root, "schemaVersion") is { } schema &&
                schema.TryGetInt32(out int value)
                    ? value
                    : null;
            bool valid = schemaVersion == 1 && !string.IsNullOrWhiteSpace(threadId);
            return new LegacyResumeImport(savedAt, digest, schemaVersion, threadId, valid,
                valid ? "LegacyScopeUnverified" : "Legacy resume document failed its schema/integrity contract.");
        }
        catch (JsonException exception)
        {
            return new LegacyResumeImport(savedAt, digest, null, null, false, exception.GetType().Name);
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

        if (!legacy.Valid)
        {
            return;
        }

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

    private sealed record LegacyResumeImport(
        string SavedAt,
        string Digest,
        int? SchemaVersion,
        string? ThreadId,
        bool Valid,
        string Diagnostic);

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

    private const string SchemaV9Sql = """
        CREATE TABLE IF NOT EXISTS workspace_schema_migrations(
            migration_id text primary key,
            schema_identity text not null,
            schema_family text not null,
            from_version integer not null,
            to_version integer not null,
            workspace_id text not null,
            applied_at text not null
        );

        CREATE TABLE IF NOT EXISTS workspace_identity_metadata(
            id integer primary key check (id = 1),
            identity_format text not null,
            migration_source text not null,
            imported_at text
        );

        CREATE TABLE IF NOT EXISTS session_continuity_profiles(
            profile_digest text primary key,
            provider text not null,
            server_version text,
            schema_digest text,
            profile_json text not null,
            evidence_source text not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS decision_session_scopes(
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

        CREATE TABLE IF NOT EXISTS decision_session_lineage(
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

        CREATE TABLE IF NOT EXISTS decision_session_active(
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

        CREATE TABLE IF NOT EXISTS session_recovery_plans(
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

        CREATE TABLE IF NOT EXISTS session_recovery_attempts(
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

        CREATE TABLE IF NOT EXISTS session_recovery_sources(
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

        CREATE TABLE IF NOT EXISTS decision_session_turns(
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

        CREATE TABLE IF NOT EXISTS session_transition_correlations(
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

        CREATE TABLE IF NOT EXISTS decision_session_legacy_imports(
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

        CREATE TABLE IF NOT EXISTS history_evidence_sets(
            evidence_set_id text primary key,
            history_id text not null unique,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS history_evidence_items(
            evidence_item_id text primary key,
            evidence_set_id text not null,
            evidence_kind text not null,
            schema_version text not null,
            provider text,
            provider_thread_id text,
            provider_turn_id text,
            continuity_lineage_id text,
            recovery_attempt_id text,
            repository_commit text,
            effect_identity text,
            payload_json text not null,
            recorded_at text not null,
            foreign key(evidence_set_id) references history_evidence_sets(evidence_set_id)
        );

        CREATE TABLE IF NOT EXISTS compatibility_import_operations(
            import_id text primary key,
            source_schema_identity text,
            source_schema_family text not null,
            source_schema_version integer,
            source_digest text not null,
            plan_hash text not null,
            state text not null,
            planned_at text not null,
            started_at text,
            verified_at text,
            completed_at text,
            diagnostic_json text not null,
            unique(source_schema_family, source_digest)
        );

        CREATE TABLE IF NOT EXISTS compatibility_import_events(
            event_id text primary key,
            import_id text not null,
            state text not null,
            recorded_at text not null,
            evidence_json text not null,
            foreign key(import_id) references compatibility_import_operations(import_id)
        );

        CREATE TABLE IF NOT EXISTS canonical_projection_effects(
            effect_id text primary key,
            history_id text not null,
            target_path text not null,
            content_hash text not null,
            status text not null,
            idempotency_key text not null unique,
            planned_at text not null,
            started_at text,
            completed_at text,
            failure text
        );

        CREATE TABLE IF NOT EXISTS transition_recovery_plans(
            recovery_id text primary key,
            transition_run_id text not null,
            source_attempt_id text not null,
            classification text not null,
            action text not null,
            resulting_attempt_mode text not null,
            next_attempt_index integer not null,
            evidence_json text not null,
            preconditions_json text not null,
            planned_at text not null
        );

        CREATE TABLE IF NOT EXISTS canonical_effect_intents(
            effect_intent_id text primary key,
            transition_run_id text not null,
            attempt_id text not null,
            effect_identity text not null,
            category text not null,
            effect_order integer not null,
            idempotency_key text not null unique,
            status text not null,
            definition_json text not null,
            planned_at text not null,
            started_at text,
            completed_at text,
            failure text
        );

        CREATE TABLE IF NOT EXISTS execution_recommendation_evidence(
            recommendation_id text primary key,
            decision_product_id text not null,
            workspace_id text not null,
            run_id text not null,
            workflow_instance_id text not null,
            transition_run_id text not null,
            attempt_id text not null,
            session_id text not null,
            turn_id text not null,
            recommended_model text not null,
            recommended_effort text not null,
            rationale text not null,
            schema_version text not null,
            created_at text not null
        );

        CREATE TABLE IF NOT EXISTS runtime_profile_evaluations(
            evaluation_id text primary key,
            recommendation_id text,
            decision_product_id text not null,
            policy_id text not null,
            provider_capability_id text not null,
            provider_capability_json text not null,
            outcome text not null,
            runtime_profile_id text not null,
            effective_profile_json text not null,
            reasons_json text not null,
            evaluated_at text not null
        );

        CREATE TABLE IF NOT EXISTS prompt_dispatch_events(
            event_id integer primary key autoincrement,
            dispatch_id text not null,
            rendered_prompt_id text not null,
            persistence_id text not null,
            workspace_id text not null,
            run_id text not null,
            workflow_instance_id text not null,
            transition_run_id text not null,
            attempt_id text not null,
            runtime_profile_id text not null,
            session_id text,
            turn_id text,
            state text not null,
            recorded_at text not null,
            evidence_json text not null
        );

        CREATE TABLE IF NOT EXISTS persistence_projection_checkpoints(
            projection_identity text primary key,
            ledger_sequence integer not null,
            projected_at text not null,
            model_hash text not null
        );

        CREATE INDEX IF NOT EXISTS idx_history_evidence_items_set
            ON history_evidence_items(evidence_set_id, evidence_kind);
        CREATE UNIQUE INDEX IF NOT EXISTS idx_loop_history_history_id
            ON loop_history(history_id) WHERE history_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS idx_history_evidence_provider
            ON history_evidence_items(provider, provider_thread_id, provider_turn_id);
        CREATE INDEX IF NOT EXISTS idx_history_evidence_recovery
            ON history_evidence_items(recovery_attempt_id);
        CREATE INDEX IF NOT EXISTS idx_compatibility_import_events_operation
            ON compatibility_import_events(import_id, recorded_at);
        CREATE INDEX IF NOT EXISTS idx_projection_effects_status
            ON canonical_projection_effects(status, planned_at);
        CREATE INDEX IF NOT EXISTS idx_transition_recovery_plans_run
            ON transition_recovery_plans(transition_run_id, planned_at);
        CREATE INDEX IF NOT EXISTS idx_canonical_effect_intents_status
            ON canonical_effect_intents(status, effect_order, planned_at);
        CREATE INDEX IF NOT EXISTS idx_prompt_dispatch_events_dispatch
            ON prompt_dispatch_events(dispatch_id, event_id);
        CREATE INDEX IF NOT EXISTS idx_prompt_dispatch_events_attempt
            ON prompt_dispatch_events(attempt_id, event_id);
        CREATE INDEX IF NOT EXISTS idx_execution_recommendation_decision
            ON execution_recommendation_evidence(decision_product_id, created_at);
        CREATE INDEX IF NOT EXISTS idx_runtime_profile_evaluation_decision
            ON runtime_profile_evaluations(decision_product_id, evaluated_at);
        CREATE INDEX IF NOT EXISTS idx_decision_scope_lifecycle
            ON decision_session_scopes(lifecycle_state, scope_id);
        CREATE INDEX IF NOT EXISTS idx_decision_lineage_scope_authority
            ON decision_session_lineage(scope_id, authority_state);
        CREATE INDEX IF NOT EXISTS idx_decision_lineage_parent
            ON decision_session_lineage(parent_lineage_id);
        CREATE INDEX IF NOT EXISTS idx_recovery_attempt_scope_status
            ON session_recovery_attempts(scope_id, status, updated_at);
        CREATE INDEX IF NOT EXISTS idx_decision_turn_transition
            ON decision_session_turns(transition_run_id);
        """;

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
            history_id text,
            run_id text,
            transition_run_id text,
            attempt_id text,
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
            evidence_json text not null,
            transition_run_id text
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
            created_at text not null,
            transition_run_id text
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

        CREATE TABLE IF NOT EXISTS canonical_chain_boundary_events(
            boundary_id text primary key,
            run_id text,
            chain_identity text not null,
            source_workflow text not null,
            target_workflow text,
            exit_gate_status text not null,
            entry_gate_status text,
            transfer_gate_status text,
            decision text not null,
            explanation text not null,
            evidence_json text not null,
            boundary_json text not null,
            recorded_at text not null
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
        CREATE INDEX IF NOT EXISTS idx_canonical_chain_boundary_events_run ON canonical_chain_boundary_events(run_id);
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
            policy_id text,
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
            completed_at text,
            effort text,
            sandbox text
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
            consumed_at text not null,
            transition_run_id text
        );

        CREATE TABLE IF NOT EXISTS canonical_policy_resolutions(
            resolution_id text primary key,
            policy_id text not null,
            schema_version text not null,
            resolved_json text not null,
            provenance_json text not null,
            source_description text not null,
            recorded_at text not null
        );

        CREATE INDEX IF NOT EXISTS idx_canonical_policy_resolutions_policy ON canonical_policy_resolutions(policy_id);

        CREATE TABLE IF NOT EXISTS canonical_rendered_prompts(
            rendered_prompt_id text primary key,
            transition_run_id text not null,
            attempt_id text,
            session_id text,
            turn_id text,
            prompt_identity text not null,
            template_source_hash text,
            rendered_sha256 text not null,
            rendered_text text not null,
            consumed_inputs_json text not null,
            policy_id text,
            rendered_at text not null
        );

        CREATE INDEX IF NOT EXISTS idx_canonical_rendered_prompts_transition ON canonical_rendered_prompts(transition_run_id);

        UPDATE canonical_workflow_states SET state = 'Resumable' WHERE state = 'Blocked';
        UPDATE canonical_workflow_states SET outcome = 'MissingRequiredInput' WHERE outcome = 'Blocked';
        UPDATE canonical_stage_states SET state = 'Resumable' WHERE state = 'Blocked';
        UPDATE canonical_transition_runs SET state = 'InputUnsatisfied' WHERE state = 'Blocked';
        UPDATE canonical_transition_runs SET outcome = 'MissingRequiredInput' WHERE outcome = 'Blocked';
        """;
}
