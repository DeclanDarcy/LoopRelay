using System.Globalization;
using System.Security.Cryptography;
using LoopRelay.Core.Models.Identity;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Services.Persistence;

public sealed record WorkspaceCompatibilityImportResult(
    string ImportId,
    string SourceDigest,
    string TargetDatabasePath,
    WorkspaceSchemaInspection SourceSchema,
    int TargetSchemaVersion);

/// <summary>
/// Explicit shadow importer for the branch-local LegacyContinuity v3 family. The source is
/// opened read-only and never rewritten. The returned v9 target still requires caller-owned
/// verification and atomic replacement of the workspace database.
/// </summary>
public static class LegacyContinuityWorkspaceImporter
{
    public static async Task<WorkspaceCompatibilityImportResult> ImportToShadowAsync(
        string sourceDatabasePath,
        string targetDatabasePath,
        CancellationToken cancellationToken = default)
    {
        string sourcePath = Path.GetFullPath(sourceDatabasePath);
        string targetPath = Path.GetFullPath(targetDatabasePath);
        if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Compatibility import requires a distinct shadow target.", nameof(targetDatabasePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Legacy workspace database was not found.", sourcePath);
        }

        if (File.Exists(targetPath))
        {
            throw new IOException($"Compatibility import target already exists: {targetPath}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        WorkspaceSchemaInspection sourceInspection;
        await using (SqliteConnection source = LoopRelayWorkspaceDatabase.OpenReadOnly(sourcePath))
        {
            await source.OpenAsync(cancellationToken);
            sourceInspection = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(source, cancellationToken);
            if (sourceInspection.Family != WorkspaceSchemaFamily.LegacyContinuity)
            {
                throw new InvalidOperationException(
                    $"Expected LegacyContinuity source, observed {sourceInspection.Family}: {sourceInspection.Diagnostic}");
            }

            await using SqliteConnection target = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(targetPath);
            await target.OpenAsync(cancellationToken);
            source.BackupDatabase(target);
        }

        string sourceDigest;
        await using (FileStream stream = new(sourcePath, FileMode.Open, FileAccess.Read,
                         FileShare.ReadWrite | FileShare.Delete, 64 * 1024,
                         FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            sourceDigest = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
        }

        string importId = CausalUlid.NewId("import");
        DateTimeOffset plannedAt = DateTimeOffset.UtcNow;
        await using SqliteConnection shadow = LoopRelayWorkspaceDatabase.OpenReadWrite(targetPath);
        await shadow.OpenAsync(cancellationToken);
        await ExecuteAsync(
            shadow,
            """
            CREATE TABLE IF NOT EXISTS compatibility_import_bootstrap(
                import_id text primary key,
                source_family text not null,
                source_version integer,
                source_digest text not null,
                state text not null,
                recorded_at text not null
            );
            INSERT INTO compatibility_import_bootstrap (
                import_id, source_family, source_version, source_digest, state, recorded_at
            ) VALUES ($id, 'LegacyContinuity', 3, $digest, 'Started', $at);
            INSERT INTO schema_metadata (key, value) VALUES ('schema_identity', $identity)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            INSERT INTO schema_metadata (key, value) VALUES ('schema_family', $family)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            cancellationToken,
            ("$id", importId),
            ("$digest", sourceDigest),
            ("$at", plannedAt.ToString("O", CultureInfo.InvariantCulture)),
            ("$identity", LoopRelayWorkspaceDatabase.SchemaIdentity),
            ("$family", LoopRelayWorkspaceDatabase.SchemaFamily));

        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(shadow, cancellationToken);
        string planHash = Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"LegacyContinuity:3->{LoopRelayWorkspaceDatabase.CurrentSchemaVersion}:{sourceDigest}")));
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        await ExecuteAsync(
            shadow,
            """
            INSERT INTO compatibility_import_operations (
                import_id, source_schema_identity, source_schema_family, source_schema_version,
                source_digest, plan_hash, state, planned_at, started_at, verified_at,
                completed_at, diagnostic_json
            ) VALUES (
                $id, 'looprelay.legacy-continuity', 'LegacyContinuity', 3,
                $digest, $plan, 'Completed', $planned, $planned, $completed,
                $completed, '{}'
            );
            INSERT INTO compatibility_import_events (event_id, import_id, state, recorded_at, evidence_json)
                VALUES ($planned_event, $id, 'Planned', $planned, '[]');
            INSERT INTO compatibility_import_events (event_id, import_id, state, recorded_at, evidence_json)
                VALUES ($started_event, $id, 'Started', $planned, '[]');
            INSERT INTO compatibility_import_events (event_id, import_id, state, recorded_at, evidence_json)
                VALUES ($verified_event, $id, 'Verified', $completed, '[]');
            INSERT INTO compatibility_import_events (event_id, import_id, state, recorded_at, evidence_json)
                VALUES ($completed_event, $id, 'Completed', $completed, '[]');
            INSERT INTO workspace_metadata (key, value) VALUES ('persistence_state', 'canonical')
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            DROP TABLE compatibility_import_bootstrap;
            """,
            cancellationToken,
            ("$id", importId),
            ("$digest", sourceDigest),
            ("$plan", planHash),
            ("$planned", plannedAt.ToString("O", CultureInfo.InvariantCulture)),
            ("$completed", completedAt.ToString("O", CultureInfo.InvariantCulture)),
            ("$planned_event", CausalUlid.NewId("impevent")),
            ("$started_event", CausalUlid.NewId("impevent")),
            ("$verified_event", CausalUlid.NewId("impevent")),
            ("$completed_event", CausalUlid.NewId("impevent")));

        return new WorkspaceCompatibilityImportResult(
            importId,
            sourceDigest,
            targetPath,
            sourceInspection,
            LoopRelayWorkspaceDatabase.CurrentSchemaVersion);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
