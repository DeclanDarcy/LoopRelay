using CommandCenter.Core.Repositories;
using Microsoft.Data.Sqlite;

namespace CommandCenter.Persistence.Sqlite.Abstractions;

/// <summary>
/// Opens SQLite connections for the two DB scopes the Derivation Cache uses:
/// <list type="bullet">
///   <item>the per-repo <c>&lt;repo&gt;/.agents/derived-cache.db</c> (co-located with the
///   <c>.md</c> contract plane; dropped with the <c>.agents</c> tree on DELETE-repo teardown);</item>
///   <item>the single global <c>command-center.db</c> recovery ledger (must survive per-repo dir churn).</item>
/// </list>
/// Every connection has the WAL / busy_timeout / synchronous / foreign_keys PRAGMAs applied on open,
/// and the schema is ensured idempotently (guarded by <c>PRAGMA user_version</c>) the first time a
/// given DB is touched. The global DB path is resolved from a <c>COMMAND_CENTER_DB_PATH</c> env var
/// (with a sensible per-user-AppData default), mirroring the configuration-path mechanism so dev,
/// test, and prod DBs are separable.
/// </summary>
public interface ISqliteConnectionFactory
{
    /// <summary>
    /// Opens (and schema-ensures) a connection to <paramref name="repo"/>'s per-repo derived-cache DB.
    /// The caller owns disposal.
    /// </summary>
    Task<SqliteConnection> OpenRepositoryConnectionAsync(Repository repo, CancellationToken ct);

    /// <summary>
    /// Opens (and schema-ensures) a connection to the global recovery-ledger DB. The caller owns disposal.
    /// </summary>
    Task<SqliteConnection> OpenGlobalConnectionAsync(CancellationToken ct);

    /// <summary>
    /// Resolves the absolute on-disk path of <paramref name="repo"/>'s per-repo derived-cache DB
    /// (<c>&lt;repo&gt;/.agents/derived-cache.db</c>).
    /// </summary>
    string GetRepositoryDatabasePath(Repository repo);

    /// <summary>
    /// Resolves the absolute on-disk path of the global recovery-ledger DB.
    /// </summary>
    string GetGlobalDatabasePath();
}
