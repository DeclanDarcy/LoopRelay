using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Completion.Services.ArtifactStorage;

internal static class CompletionLogicalArtifactServices
{
    public static ILogicalArtifactResolver CreateResolver(CompletionArtifacts artifacts) =>
        new LogicalArtifactResolver(
        [
            new SqliteCompletionLoopHistoryLogicalArtifactProvider(artifacts.Repository),
            new FileBackedExecutionEvidenceLogicalArtifactProvider(artifacts.ExecutionEvidenceStore),
        ]);
}

internal sealed class SqliteCompletionLoopHistoryLogicalArtifactProvider(Repository repository) : ILogicalArtifactProvider
{
    private const string RelativeDatabasePath = ".LoopRelay/persistence/looprelay.sqlite3";

    public bool CanResolve(string relativePath)
    {
        string normalized = Normalize(relativePath);
        return normalized.StartsWith(CompletionArtifactPaths.DecisionsDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(CompletionArtifactPaths.HandoffsDirectory + "/", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(CompletionArtifactPaths.DeltasDirectory + "/", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<LogicalArtifactResolutionResult> ResolveAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        string normalized = Normalize(relativePath);
        var descriptor = new LogicalArtifactDescriptor(
            normalized,
            LogicalArtifactDomain.LoopHistory,
            LogicalArtifactStorageKind.SqliteCanonicalRecord,
            normalized);
        string databasePath = ResolveDatabase(repository);
        if (!File.Exists(databasePath))
        {
            return LogicalArtifactResolutionResult.Unresolved(
                descriptor,
                LogicalArtifactResolutionStatus.MissingMigratedRecord,
                $"SQLite loop history database is missing for: {normalized}");
        }

        await using SqliteConnection connection = OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        if (!await TableExistsAsync(connection, "loop_history", cancellationToken))
        {
            return LogicalArtifactResolutionResult.Unresolved(
                descriptor,
                LogicalArtifactResolutionStatus.MissingMigratedRecord,
                $"SQLite loop history table is missing for: {normalized}");
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT body FROM loop_history WHERE logical_path = $logical_path;";
        command.Parameters.AddWithValue("$logical_path", normalized);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull
            ? LogicalArtifactResolutionResult.Unresolved(
                descriptor,
                LogicalArtifactResolutionStatus.MissingMigratedRecord,
                $"SQLite loop history record is missing: {normalized}")
            : LogicalArtifactResolutionResult.Resolved(descriptor, Convert.ToString(scalar)!);
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(scalar) == 1;
    }

    private static string ResolveDatabase(Repository repository)
    {
        string workspaceRoot = Path.GetFullPath(repository.Path);
        string databasePath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            RelativeDatabasePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(workspaceRoot, databasePath);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException("Resolved workspace database path escaped the repository root.");
        }

        return databasePath;
    }

    private static SqliteConnection OpenReadOnly(string databasePath) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim().TrimStart('/');
}
