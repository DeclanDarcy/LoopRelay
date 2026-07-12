using System.Globalization;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Execution;

internal static class LoopWorkspaceDatabase
{
    public const int CurrentSchemaVersion = LoopRelayWorkspaceDatabase.CurrentSchemaVersion;
    public const string RelativeDatabasePath = LoopRelayWorkspaceDatabase.RelativeDatabasePath;

    public static string Resolve(Repository repository) =>
        LoopRelayWorkspaceDatabase.Resolve(repository);

    public static bool HasUsableLoopHistoryDatabase(Repository repository)
    {
        string databasePath = Resolve(repository);
        if (!File.Exists(databasePath))
        {
            return false;
        }

        try
        {
            using SqliteConnection connection = OpenReadOnly(databasePath);
            connection.Open();
            string? version = ScalarString(
                connection,
                "SELECT value FROM schema_metadata WHERE key = 'schema_version';");
            if (!int.TryParse(version, NumberStyles.Integer, CultureInfo.InvariantCulture, out int schemaVersion) ||
                schemaVersion != CurrentSchemaVersion)
            {
                return false;
            }

            if (!string.Equals(
                    ScalarString(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_identity';"),
                    LoopRelayWorkspaceDatabase.SchemaIdentity,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ScalarString(connection, "SELECT value FROM schema_metadata WHERE key = 'schema_family';"),
                    LoopRelayWorkspaceDatabase.SchemaFamily,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string? state = ScalarString(
                connection,
                "SELECT value FROM workspace_metadata WHERE key = 'persistence_state';");
            if (state is not "imported" and not "canonical")
            {
                return false;
            }

            long tables = ScalarLong(
                connection,
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'loop_history';");
            return tables == 1;
        }
        catch (SqliteException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    internal static SqliteConnection OpenReadOnly(string databasePath) =>
        LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);

    internal static SqliteConnection OpenReadWrite(string databasePath) =>
        LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);

    private static string? ScalarString(SqliteConnection connection, string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = command.ExecuteScalar();
        return scalar is null or DBNull ? null : Convert.ToString(scalar, CultureInfo.InvariantCulture);
    }

    private static long ScalarLong(SqliteConnection connection, string commandText)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = command.ExecuteScalar();
        return Convert.ToInt64(scalar, CultureInfo.InvariantCulture);
    }
}
