using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Tests.Services;

/// <summary>
/// Covers PERF-01: <see cref="LoopRelayWorkspaceDatabase.EnsureSchemaAsync"/> must memoize a
/// successful full verification per (path, stamp) so repeat calls on an unchanged, already
/// canonical-v15 database skip both the ~190-probe structural inspection and any write
/// transaction, while still detecting a stamp that has moved and still enforcing fail-closed
/// behavior on first contact.
/// </summary>
[Collection("WorkspaceDatabaseCounters")]
public sealed class LoopRelayWorkspaceDatabaseEnsureTests
{
    [Fact]
    public async Task EnsureSchema_OnHealthyDb_SecondCallSkipsFullVerification()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        }

        // Reset simulates the perspective of "nothing verified yet in this process" so the next
        // call is unambiguously the first-contact full verification, and the one after that is
        // unambiguously the memoized fast path.
        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        int baseline = LoopRelayWorkspaceDatabase.FullVerificationRuns;

        await using (SqliteConnection second = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await second.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(second);
        }

        Assert.Equal(baseline + 1, LoopRelayWorkspaceDatabase.FullVerificationRuns);

        await using (SqliteConnection third = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await third.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(third);
        }

        Assert.Equal(baseline + 1, LoopRelayWorkspaceDatabase.FullVerificationRuns);
    }

    [Fact]
    public async Task EnsureSchema_OnHealthyDb_PerformsNoWrite()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        }

        // A second, independently open connection observes `PRAGMA data_version`, which only
        // increments when some OTHER connection commits a change to the file. If the memoized
        // fast path below opens a transaction (even a no-op UPDATE/DROP), this connection will see it.
        await using SqliteConnection observer = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await observer.OpenAsync();
        long before = await ScalarLongAsync(observer, "PRAGMA data_version;");

        await using (SqliteConnection third = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await third.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(third);
        }

        long after = await ScalarLongAsync(observer, "PRAGMA data_version;");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task EnsureSchema_StampMismatch_RerunsFullVerification()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        int baseline = LoopRelayWorkspaceDatabase.FullVerificationRuns;

        // Tamper the stamped fingerprint directly (without touching the physical shape). This
        // must invalidate the memo and force a full re-verification pass, which - matching
        // today's unmemoized behavior for a stamp that no longer matches the observed structural
        // fingerprint - rejects the database as an unrecognized/corrupt shape.
        await ExecuteAsync(connection, "UPDATE schema_metadata SET value = 'tampered-fingerprint' WHERE key = 'schema_shape';");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));

        Assert.Equal(baseline + 1, LoopRelayWorkspaceDatabase.FullVerificationRuns);
    }

    [Fact]
    public async Task Database_UsesBusyTimeout()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        // First writable contact: this is the (process, path) first-contact call that must set
        // busy_timeout on this very connection. (WAL mode is deferred - see the Core performance
        // remediation plan's Decisions section - so it is not asserted here.)
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

            Assert.Equal(
                LoopRelayWorkspaceDatabase.BusyTimeoutMilliseconds,
                await ScalarLongAsync(connection, "PRAGMA busy_timeout;"));
        }

        // A brand-new connection to the same path, in the same process, after the memoized fast
        // path is in play: busy_timeout is per-connection state so it must be (re-)applied on this
        // fresh connection too.
        await using (SqliteConnection fresh = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await fresh.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(fresh);

            Assert.Equal(
                LoopRelayWorkspaceDatabase.BusyTimeoutMilliseconds,
                await ScalarLongAsync(fresh, "PRAGMA busy_timeout;"));
        }
    }

    [Fact]
    public async Task EnsureSchema_OnHealthyDb_MemoizedFastPath_OpensNoWriteTransaction()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        string walPath = databasePath + "-wal";
        string shmPath = databasePath + "-shm";

        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        }

        // The migration connection above ran the real (migrationTransaction) branch, not
        // RunStructurallyCompleteBranchAsync, so it never touches RepairTransactionsOpened. Reset
        // it here purely so the counter below reflects only the memoized fast-path call.
        LoopRelayWorkspaceDatabase.ResetSchemaVerificationCacheForTesting();
        await using (SqliteConnection reverify = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await reverify.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(reverify);
        }
        int baseline = LoopRelayWorkspaceDatabase.RepairTransactionsOpened;

        // The migration connection above is now fully closed. Whatever WAL side-file state that
        // leaves behind is the baseline this test cares about preserving - not asserting it is
        // empty (that is a property of SQLite's own checkpoint-on-close behavior, not of this
        // regression), but capturing it so the memoized fast path below can be held to "did not
        // change this" rather than to an assumption about what the baseline should be.
        long walLengthBefore = File.Exists(walPath) ? new FileInfo(walPath).Length : -1;
        long shmLengthBefore = File.Exists(shmPath) ? new FileInfo(shmPath).Length : -1;

        // A brand-new connection/open on the same path: with the in-process memo already
        // populated from the calls above, this is unambiguously the memoized fast path
        // (RunStructurallyCompleteBranchAsync). On a genuinely healthy database - no legacy
        // resume pending, no stray legacy 'Blocked' vocabulary anywhere - this must not open any
        // write transaction at all. RepairTransactionsOpened is the direct, unambiguous signal for
        // that (see its doc comment: neither PRAGMA data_version nor -wal file length reliably
        // distinguishes a zero-row-affecting transaction from no transaction at all on this
        // codebase's SQLite/Microsoft.Data.Sqlite version - verified empirically). The -wal/-shm
        // length assertions below are kept as secondary, real-signal corroboration of the same
        // "nothing was touched" claim once the counter has already proven no transaction opened.
        await using (SqliteConnection second = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await second.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(second);
        }

        Assert.Equal(baseline, LoopRelayWorkspaceDatabase.RepairTransactionsOpened);

        long walLengthAfter = File.Exists(walPath) ? new FileInfo(walPath).Length : -1;
        long shmLengthAfter = File.Exists(shmPath) ? new FileInfo(shmPath).Length : -1;

        Assert.Equal(walLengthBefore, walLengthAfter);
        Assert.Equal(shmLengthBefore, shmLengthAfter);
    }

    [Fact]
    public async Task EnsureSchema_MemoizedFastPath_StillRepairsStrayLegacyBlockedVocabulary()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        }

        // Insert a stray legacy 'Blocked' row directly, bypassing EnsureSchemaAsync entirely, the
        // same way an out-of-band writer (or a pre-migration artifact) could leave one behind.
        // This must be visible to - and repaired by - the very next EnsureSchemaAsync call, even
        // though the in-process memo for this path is already populated and that next call is
        // therefore the memoized fast path, not a full verification pass.
        await using (SqliteConnection tamper = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await tamper.OpenAsync();
            await ExecuteAsync(
                tamper,
                """
                INSERT INTO canonical_workflow_states
                    (workflow_identity, state, current_stage, outcome, updated_at, evidence_json)
                VALUES
                    ('wf-stray-blocked', 'Blocked', NULL, NULL, '2026-01-01T00:00:00Z', '{}');
                """);
        }

        await using (SqliteConnection third = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await third.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(third);
        }

        await using SqliteConnection verify = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);
        await verify.OpenAsync();
        string? repairedState = await ScalarStringAsync(
            verify,
            "SELECT state FROM canonical_workflow_states WHERE workflow_identity = 'wf-stray-blocked';");

        Assert.Equal("Resumable", repairedState);
    }

    [Fact]
    public async Task EnsureSchema_LegacyContinuity_StillThrowsImportRequired()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await ExecuteAsync(
            connection,
            """
            CREATE TABLE schema_metadata(key text primary key, value text not null);
            INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '3');
            CREATE TABLE session_continuity_profiles(profile_digest text primary key);
            CREATE TABLE decision_session_scopes(scope_id text primary key);
            """);

        await Assert.ThrowsAsync<WorkspaceCompatibilityImportRequiredException>(
            () => LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection));
    }

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-schema-ensure-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }

    private static string CreateDatabasePath(Repository repository)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        return databasePath;
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt64(scalar);
    }

    private static async Task<string?> ScalarStringAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }
}
