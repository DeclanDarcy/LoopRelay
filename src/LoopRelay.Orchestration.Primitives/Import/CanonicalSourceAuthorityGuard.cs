using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Import;

public static class CanonicalSourceAuthorityGuard
{
    public static bool IsCanonicalOnly(string databasePath)
    {
        if (!File.Exists(databasePath)) return false;
        try
        {
            using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(databasePath);
            connection.Open();
            using SqliteCommand exists = connection.CreateCommand();
            exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='canonical_source_authority';";
            if (Convert.ToInt64(exists.ExecuteScalar()) != 1) return false;
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT canonical_only FROM canonical_source_authority WHERE id=1;";
            return Convert.ToInt64(command.ExecuteScalar()) == 1;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    public static void RejectLegacyReader(string databasePath, string readerIdentity)
    {
        if (IsCanonicalOnly(databasePath))
            throw new InvalidOperationException(
                $"Legacy reader `{readerIdentity}` was invoked after the monotonic canonical-only marker.");
    }
}
