using System.Collections.Concurrent;
using LoopRelay.Core.Repositories;
using LoopRelay.Persistence.Sqlite.Abstractions;
using LoopRelay.Persistence.Sqlite.Schema;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Persistence.Sqlite;

/// <summary>
/// Default <see cref="ISqliteConnectionFactory"/>: opens connections to the per-repo derived-cache DB
/// and the global recovery-ledger DB, applies the WAL/busy_timeout/synchronous/foreign_keys PRAGMAs
/// on open, and lazily ensures the schema (idempotent, guarded by <c>PRAGMA user_version</c>) the first
/// time a given DB file is touched in this process.
/// </summary>
public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly SqliteDatabaseOptions options;

    // Remembers which DB paths have already had their schema ensured this process, so EnsureSchemaAsync's
    // user_version probe + DDL only runs once per file rather than on every connection open.
    private readonly ConcurrentDictionary<string, bool> ensured =
        new(StringComparer.OrdinalIgnoreCase);

    public SqliteConnectionFactory(SqliteDatabaseOptions options)
    {
        this.options = options;
    }

    public string GetGlobalDatabasePath() => options.GlobalDatabasePath;

    public string GetRepositoryDatabasePath(Repository repo)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return System.IO.Path.GetFullPath(
            System.IO.Path.Combine(repo.Path, options.RepositoryDatabaseRelativePath));
    }

    public Task<SqliteConnection> OpenRepositoryConnectionAsync(Repository repo, CancellationToken ct)
        => OpenAsync(GetRepositoryDatabasePath(repo), DerivedCacheSchema.PerRepositoryTables, ct);

    public Task<SqliteConnection> OpenGlobalConnectionAsync(CancellationToken ct)
        => OpenAsync(GetGlobalDatabasePath(), DerivedCacheSchema.GlobalTables, ct);

    private async Task<SqliteConnection> OpenAsync(string databasePath, string tablesDdl, CancellationToken ct)
    {
        string? directory = System.IO.Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString());

        await connection.OpenAsync(ct).ConfigureAwait(false);

        await ExecuteAsync(connection, DerivedCacheSchema.ConnectionPragmas, ct).ConfigureAwait(false);

        if (!ensured.ContainsKey(databasePath))
        {
            await EnsureSchemaAsync(connection, tablesDdl, ct).ConfigureAwait(false);
            ensured[databasePath] = true;
        }

        return connection;
    }

    /// <summary>
    /// Creates the tables for this DB (via <c>CREATE TABLE IF NOT EXISTS</c>) and bumps
    /// <c>PRAGMA user_version</c>, but only when the stored version is below the current schema
    /// version. Idempotent: running it against an up-to-date DB is a no-op probe.
    /// </summary>
    private static async Task EnsureSchemaAsync(SqliteConnection connection, string tablesDdl, CancellationToken ct)
    {
        long current;
        await using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "PRAGMA user_version;";
            object? raw = await probe.ExecuteScalarAsync(ct).ConfigureAwait(false);
            current = raw is null ? 0 : Convert.ToInt64(raw);
        }

        if (current >= DerivedCacheSchema.UserVersion)
        {
            return;
        }

        await ExecuteAsync(connection, tablesDdl, ct).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            $"PRAGMA user_version = {DerivedCacheSchema.UserVersion};",
            ct).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
