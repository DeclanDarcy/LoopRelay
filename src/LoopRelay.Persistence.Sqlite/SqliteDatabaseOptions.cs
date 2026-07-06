namespace LoopRelay.Persistence.Sqlite;

/// <summary>
/// Resolves the global DB path, mirroring <c>ApplicationConfigurationStore</c>'s env-var mechanism:
/// <c>COMMAND_CENTER_DB_PATH</c> overrides the default <c>%AppData%/LoopRelay/command-center.db</c>.
/// Scoping the path by env var is what keeps a Release test host and a live Debug backend isolated
/// (the build-Release-while-Debug-runs constraint) and lets each test point at its own temp DB.
/// </summary>
public sealed class SqliteDatabaseOptions
{
    public SqliteDatabaseOptions()
        : this(Environment.GetEnvironmentVariable("COMMAND_CENTER_DB_PATH") ??
            System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LoopRelay",
                "command-center.db"))
    {
    }

    public SqliteDatabaseOptions(string globalDatabasePath)
    {
        GlobalDatabasePath = globalDatabasePath;
    }

    /// <summary>Absolute path of the global recovery-ledger DB.</summary>
    public string GlobalDatabasePath { get; }

    /// <summary>
    /// The relative path of a repo's derived-cache DB under its root
    /// (<c>.agents/derived-cache.db</c>).
    /// </summary>
    public string RepositoryDatabaseRelativePath { get; init; } =
        System.IO.Path.Combine(".agents", "derived-cache.db");
}
