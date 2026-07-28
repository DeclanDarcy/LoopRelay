using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Telemetry;

/// <summary>
/// Canonical SQLite sink for per-turn session telemetry events.
///
/// <para>
/// <b>Cached-state design (PERF task w2-t4):</b> directory creation, the gitignore probe, and the
/// (structural, migration-capable) schema-ensure pipeline only need to run once per sink
/// instance - <see cref="schemaEnsured"/> tracks that under <see cref="gate"/>. Connections
/// themselves stay short-lived (opened and disposed per append, as before) rather than being
/// cached, deliberately: a persistent connection held for the sink's lifetime would need explicit
/// disposal wiring the surrounding composition doesn't have today, and would turn "the workspace
/// database was deleted/moved out from under us" into a much harder failure to recover from than
/// "the next append opens a fresh connection." A slower, safer design was chosen over the faster
/// one - see <see cref="ApplyRequiredPragmas"/> for the one correctness wrinkle that follows from
/// still opening a new connection on every append.
/// </para>
///
/// <para>
/// <b>Dependency on Core's stamp re-read (do not narrow the catch below):</b>
/// <see cref="LoopRelayWorkspaceDatabase.EnsureSchemaAsync"/> memoizes verified schemas
/// process-wide but, by design, still re-reads the stamped schema version on every single call as
/// a cheap concession to the unresolved multi-process question. <see cref="schemaEnsured"/> skips
/// even that cheap re-read for appends 2..N of this sink - once true, this sink calls
/// <see cref="ApplyRequiredPragmas"/> instead of <c>EnsureSchemaAsync</c> and never asks Core to
/// re-check the stamp again. That is an accepted risk (telemetry is fail-open at the
/// <c>SessionTelemetryRecorder</c> boundary, which catches and warns; the sink itself rethrows, so
/// a persistently broken store is reported rather than silently losing rows), but only because the
/// broad <c>catch (SqliteException)</c> in <see cref="Append"/> is
/// this sink's sole remaining self-healing path: it is what resets <see cref="schemaEnsured"/> so
/// the next append re-verifies from scratch if the schema ever drifted out from under the cached
/// flag. Narrowing that catch to exclude insert-level errors would remove that recovery path
/// entirely - do not narrow it.
/// </para>
/// </summary>
internal sealed class SqliteSessionTelemetrySink(Repository repository) : ISessionTelemetrySink
{
    private readonly object gate = new();

    /// <summary>
    /// Guards the one-time directory/gitignore/schema-ensure block under <see cref="gate"/>.
    /// Deliberately per-instance (not static): each sink instance owns its own cached state, so
    /// concurrent test classes (or concurrent sinks pointed at different repositories) can never
    /// make this flake, and no <c>[Collection(DisableParallelization = true)]</c> is needed.
    /// Left <c>false</c> whenever the last init attempt failed, so a later append retries rather
    /// than being permanently poisoned by a transient IO failure.
    /// </summary>
    private bool schemaEnsured;

#if DEBUG
    /// <summary>Test-only observability: how many times this instance has run the full
    /// directory-create/gitignore/schema-ensure block. The load-bearing assertion for this task is
    /// that two appends leave this at 1, not 2.</summary>
    internal int InitializationCount { get; private set; }
#endif

    private string RuntimeDirectoryPath => Path.Combine(repository.Path, ".LoopRelay");

    private string DatabasePath => LoopRelayWorkspaceDatabase.Resolve(repository);

    public void Append(SessionTelemetryRecord record)
    {
        string json = JsonSerializer.Serialize(record, SessionTelemetryJson.Options);
        lock (gate)
        {
            try
            {
                if (!schemaEnsured)
                {
                    Directory.CreateDirectory(RuntimeDirectoryPath);
                    EnsureSelfIgnore();
                    Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
                }

                using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(DatabasePath);
                connection.Open();

                if (!schemaEnsured)
                {
                    // Structural verification/migration - the expensive part - only ever needs to
                    // run once per sink lifetime. This also applies the two PRAGMAs below as a
                    // side effect for this first connection.
                    LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection).GetAwaiter().GetResult();
                    schemaEnsured = true;
#if DEBUG
                    InitializationCount++;
#endif
                }
                else
                {
                    ApplyRequiredPragmas(connection);
                }

                InsertRow(connection, record, json);
            }
            catch (IOException)
            {
                // Reset the cached "ready" state so the next append retries full init from
                // scratch, then propagate: SessionTelemetryRecorder owns the "Session telemetry
                // not recorded" warning, and CompositeSessionTelemetrySink runs every other sink
                // before rethrowing, so a broken store is visible without starving its sibling.
                schemaEnsured = false;
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                schemaEnsured = false;
                throw;
            }
            catch (SqliteException)
            {
                // Deliberately broad: this is the only self-healing path for schemaEnsured (see
                // the type-level doc comment). It fires for insert-level SQLite errors too, not
                // just schema-verification ones - that is intentional, not a bug. Do not narrow
                // this to a schema-specific SQLite error code; doing so would leave a drifted or
                // corrupted schema permanently cached as "ensured" until the process restarts.
                schemaEnsured = false;
                throw;
            }
        }
    }

    /// <summary>
    /// Mirrors exactly the two PRAGMA statements <see cref="LoopRelayWorkspaceDatabase.EnsureSchemaAsync"/>
    /// applies unconditionally at the top of every call, because that state is per-connection and
    /// not persisted in the database file (see that method's own remarks). Once this sink has
    /// verified the schema once, later appends still open a fresh connection per call (kept
    /// short-lived deliberately, per the type-level design note), which still needs these two
    /// statements even though it does not need the far more expensive stamp/legacy-resume/repair
    /// checks <c>EnsureSchemaAsync</c> also performs on every call.
    ///
    /// <para>
    /// <b><see cref="LoopRelayWorkspaceDatabase"/> is the source of truth for this pragma set</b>
    /// (currently <c>PRAGMA foreign_keys</c> and <c>PRAGMA busy_timeout</c>, applied at the top of
    /// <c>EnsureSchemaAsync</c> in <c>src/LoopRelay.Core/Services/Persistence/LoopRelayWorkspaceDatabase.cs</c>).
    /// Nothing enforces that this hand-copy stays in sync - if Core's per-connection pragma set
    /// ever changes (e.g. a pragma added to enable WAL), this method must be re-checked and
    /// updated to match.
    /// </para>
    /// </summary>
    private static void ApplyRequiredPragmas(SqliteConnection connection)
    {
        using SqliteCommand foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys = ON;";
        foreignKeys.ExecuteNonQuery();

        using SqliteCommand busyTimeout = connection.CreateCommand();
        busyTimeout.CommandText =
            $"PRAGMA busy_timeout = {LoopRelayWorkspaceDatabase.BusyTimeoutMilliseconds};";
        busyTimeout.ExecuteNonQuery();
    }

    private static void InsertRow(SqliteConnection connection, SessionTelemetryRecord record, string json)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO session_telemetry_events (
                recorded_at,
                repo_name,
                session_id,
                session_type,
                turn_index,
                document_json,
                content_hash)
            VALUES (
                $recorded_at,
                $repo_name,
                $session_id,
                $session_type,
                $turn_index,
                $document_json,
                $content_hash);
            """;
        command.Parameters.AddWithValue("$recorded_at", record.Timestamp.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$repo_name", record.RepoName);
        command.Parameters.AddWithValue("$session_id", record.SessionId);
        command.Parameters.AddWithValue("$session_type", record.SessionType);
        command.Parameters.AddWithValue("$turn_index", record.TurnIndex);
        command.Parameters.AddWithValue("$document_json", json);
        command.Parameters.AddWithValue("$content_hash", Sha256(json));
        command.ExecuteNonQuery();
    }

    private void EnsureSelfIgnore()
    {
        string gitignore = Path.Combine(RuntimeDirectoryPath, ".gitignore");
        if (!File.Exists(gitignore))
        {
            File.WriteAllText(gitignore, "*\n");
        }
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
}
