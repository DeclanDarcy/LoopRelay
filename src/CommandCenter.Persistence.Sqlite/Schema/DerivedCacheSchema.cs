namespace CommandCenter.Persistence.Sqlite.Schema;

/// <summary>
/// The DDL for the Derivation Cache, expressed exactly as the design's "Concrete SQLite schema"
/// section. All statements are <c>CREATE TABLE IF NOT EXISTS</c> / <c>CREATE INDEX IF NOT EXISTS</c>
/// so they are idempotent; application is additionally guarded by <c>PRAGMA user_version</c> so the
/// DDL runs at most once per DB version per process touch.
/// </summary>
public static class DerivedCacheSchema
{
    /// <summary>Current schema version, written into <c>PRAGMA user_version</c>.</summary>
    public const int UserVersion = 1;

    /// <summary>The PRAGMAs applied to every connection on open.</summary>
    public const string ConnectionPragmas =
        "PRAGMA journal_mode = WAL;" +
        "PRAGMA busy_timeout = 5000;" +
        "PRAGMA synchronous = NORMAL;" +
        "PRAGMA foreign_keys = ON;";

    /// <summary>
    /// Per-repo DB tables: the derived-snapshot base cache, the per-family source fingerprint cache,
    /// and the append-only recovery-result audit (with its latest-first index).
    /// </summary>
    public const string PerRepositoryTables = """
        CREATE TABLE IF NOT EXISTS derived_snapshot (
            repository_id      TEXT NOT NULL,
            kind               TEXT NOT NULL,
            source_fingerprint TEXT NOT NULL,
            formula_version    TEXT NOT NULL,
            schema_version     TEXT NOT NULL,
            computed_at        TEXT NOT NULL,
            payload_json       TEXT NOT NULL,
            PRIMARY KEY (repository_id, kind)
        ) WITHOUT ROWID;

        CREATE TABLE IF NOT EXISTS source_fingerprint (
            repository_id  TEXT NOT NULL,
            family         TEXT NOT NULL,
            fingerprint    TEXT NOT NULL,
            row_count      INTEGER NOT NULL,
            max_updated_at TEXT,
            computed_at    TEXT NOT NULL,
            PRIMARY KEY (repository_id, family)
        );

        CREATE TABLE IF NOT EXISTS recovery_result (
            repository_id TEXT NOT NULL,
            recovery_id   TEXT NOT NULL,
            occurred_at   TEXT NOT NULL,
            payload_json  TEXT NOT NULL,
            PRIMARY KEY (repository_id, recovery_id)
        );

        CREATE INDEX IF NOT EXISTS ix_recovery_result_latest
            ON recovery_result(repository_id, occurred_at DESC);
        """;

    /// <summary>
    /// Global DB table: the recovery coordination ledger (one row per repo; survives per-repo dir churn).
    /// </summary>
    public const string GlobalTables = """
        CREATE TABLE IF NOT EXISTS recovery_ledger (
            repository_id          TEXT PRIMARY KEY,
            execution_recovered_at TEXT,
            last_assessed_at       TEXT
        );
        """;
}
