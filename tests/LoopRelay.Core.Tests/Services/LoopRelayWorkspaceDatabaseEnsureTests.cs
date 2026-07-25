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
    public async Task Database_UsesWalAndBusyTimeout()
    {
        Repository repository = CreateRepository();
        string databasePath = CreateDatabasePath(repository);

        // First writable contact: this is the (process, path) first-contact call that must flip
        // the database file itself into WAL mode (a persistent, on-disk property of the database,
        // not per-connection state) and must also set busy_timeout on this very connection.
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

            Assert.Equal("wal", await ScalarStringAsync(connection, "PRAGMA journal_mode;"));
            Assert.Equal(
                LoopRelayWorkspaceDatabase.BusyTimeoutMilliseconds,
                await ScalarLongAsync(connection, "PRAGMA busy_timeout;"));
        }

        // A brand-new connection to the same path, in the same process, after the memoized fast
        // path is in play: WAL is a database-file property so it must already be in effect
        // without re-running full verification; busy_timeout is per-connection state so it must be
        // (re-)applied on this fresh connection too.
        await using (SqliteConnection fresh = LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath))
        {
            await fresh.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(fresh);

            Assert.Equal("wal", await ScalarStringAsync(fresh, "PRAGMA journal_mode;"));
            Assert.Equal(
                LoopRelayWorkspaceDatabase.BusyTimeoutMilliseconds,
                await ScalarLongAsync(fresh, "PRAGMA busy_timeout;"));
        }
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
