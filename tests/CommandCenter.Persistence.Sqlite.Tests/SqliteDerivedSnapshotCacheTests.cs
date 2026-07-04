using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandCenter.Core.Repositories;
using CommandCenter.Persistence.Sqlite;
using CommandCenter.Persistence.Sqlite.Abstractions;
using CommandCenter.Persistence.Sqlite.Schema;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CommandCenter.Persistence.Sqlite.Tests;

/// <summary>
/// Real temp-file SQLite tests for the connection factory + Dapper cache: schema-ensure idempotency,
/// the PRAGMA application, the UPSERT, and the payload round-trip through the same serializer. These
/// open real DB files on disk under a per-test temp root (deleted in <see cref="Dispose"/>) and never
/// touch the prod DB path, mirroring DecisionSessionTestHarness's per-test temp-repo pattern. They are
/// serialized via the ProcessEnvironment collection because they boot real persistence resources.
/// </summary>
[Collection("ProcessEnvironment")]
public sealed class SqliteDerivedSnapshotCacheTests : IDisposable
{
    private sealed record Base(int Count, long Bytes, string SessionStartedAt);

    private readonly string tempRoot;
    private readonly Repository repository;
    private readonly SqliteConnectionFactory factory;
    private readonly SqliteDerivedSnapshotCache cache;

    public SqliteDerivedSnapshotCacheTests()
    {
        tempRoot = Path.Combine(Path.GetTempPath(), $"cc-sqlite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "temp",
            Path = tempRoot
        };

        var options = new SqliteDatabaseOptions(Path.Combine(tempRoot, "command-center.db"));
        factory = new SqliteConnectionFactory(options);
        var locator = new SingleRepositoryLocator(repository);
        cache = new SqliteDerivedSnapshotCache(factory, locator, TimeProvider.System);
    }

    [Fact]
    public async Task OpenRepositoryConnection_EnsuresSchema_AndCreatesTheDbFile()
    {
        await using SqliteConnection connection =
            await factory.OpenRepositoryConnectionAsync(repository, CancellationToken.None);

        IReadOnlyList<string> tables = (await connection.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type IN ('table','index') ORDER BY name;")).ToList();

        Assert.Contains("derived_snapshot", tables);
        Assert.Contains("source_fingerprint", tables);
        Assert.Contains("recovery_result", tables);
        Assert.Contains("ix_recovery_result_latest", tables);
        Assert.True(File.Exists(factory.GetRepositoryDatabasePath(repository)));
    }

    [Fact]
    public async Task OpenConnection_AppliesPragmas()
    {
        await using SqliteConnection connection =
            await factory.OpenRepositoryConnectionAsync(repository, CancellationToken.None);

        string journalMode = await connection.ExecuteScalarAsync<string>("PRAGMA journal_mode;") ?? "";
        long busyTimeout = await connection.ExecuteScalarAsync<long>("PRAGMA busy_timeout;");
        long foreignKeys = await connection.ExecuteScalarAsync<long>("PRAGMA foreign_keys;");
        long userVersion = await connection.ExecuteScalarAsync<long>("PRAGMA user_version;");

        Assert.Equal("wal", journalMode.ToLowerInvariant());
        Assert.Equal(5000, busyTimeout);
        Assert.Equal(1, foreignKeys);
        Assert.Equal(DerivedCacheSchema.UserVersion, userVersion);
    }

    [Fact]
    public async Task EnsureSchema_IsIdempotent_AcrossRepeatedOpens()
    {
        await using (await factory.OpenRepositoryConnectionAsync(repository, CancellationToken.None))
        {
        }

        await using SqliteConnection connection =
            await factory.OpenRepositoryConnectionAsync(repository, CancellationToken.None);

        long tableCount = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'derived_snapshot';");

        Assert.Equal(1, tableCount);
    }

    [Fact]
    public async Task GlobalConnection_EnsuresRecoveryLedger()
    {
        await using SqliteConnection connection =
            await factory.OpenGlobalConnectionAsync(CancellationToken.None);

        long ledger = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'recovery_ledger';");

        Assert.Equal(1, ledger);
    }

    [Fact]
    public async Task PutThenGet_RoundTripsTheBaseThroughSqlite()
    {
        var stored = new Base(12, 3456, "2026-06-28T10:00:00.0000000+00:00");

        await cache.PutAsync(repository.Id, "metrics-base", "fp-1", "v1", stored, CancellationToken.None);
        Base? roundTripped = await cache.TryGetAsync<Base>(
            repository.Id, "metrics-base", "fp-1", "v1", CancellationToken.None);

        Assert.Equal(stored, roundTripped);
    }

    [Fact]
    public async Task Put_Upserts_OneRowPerRepoAndKind()
    {
        await cache.PutAsync(repository.Id, "metrics-base", "fp-1", "v1", new Base(1, 1, "a"), CancellationToken.None);
        await cache.PutAsync(repository.Id, "metrics-base", "fp-2", "v1", new Base(2, 2, "b"), CancellationToken.None);

        await using SqliteConnection connection =
            await factory.OpenRepositoryConnectionAsync(repository, CancellationToken.None);
        long rows = await connection.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM derived_snapshot WHERE kind = 'metrics-base';");

        Base? latest = await cache.TryGetAsync<Base>(
            repository.Id, "metrics-base", "fp-2", "v1", CancellationToken.None);

        Assert.Equal(1, rows);
        Assert.Equal(new Base(2, 2, "b"), latest);
    }

    [Fact]
    public async Task TryGet_Misses_OnStaleFingerprintAndBumpedFormula()
    {
        await cache.PutAsync(repository.Id, "metrics-base", "fp-1", "v1", new Base(1, 1, "a"), CancellationToken.None);

        Base? staleFingerprint = await cache.TryGetAsync<Base>(
            repository.Id, "metrics-base", "fp-2", "v1", CancellationToken.None);
        Base? bumpedFormula = await cache.TryGetAsync<Base>(
            repository.Id, "metrics-base", "fp-1", "v2", CancellationToken.None);

        Assert.Null(staleFingerprint);
        Assert.Null(bumpedFormula);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class SingleRepositoryLocator : IRepositoryLocator
    {
        private readonly Repository repository;

        public SingleRepositoryLocator(Repository repository) => this.repository = repository;

        public Task<Repository?> FindAsync(Guid repositoryId, CancellationToken ct)
            => Task.FromResult(repositoryId == repository.Id ? repository : null);
    }
}
