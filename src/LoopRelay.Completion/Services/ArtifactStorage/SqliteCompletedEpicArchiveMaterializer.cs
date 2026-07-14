using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LoopRelay.Completion.Abstractions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Completion.Services.ArtifactStorage;

public sealed partial class SqliteCompletedEpicArchiveMaterializer(
    IReadOnlyList<string>? requiredLogicalPaths = null) : ICompletedEpicArchiveMaterializer
{
    private const string RelativeDatabasePath = ".LoopRelay/persistence/looprelay.sqlite3";
    private readonly IReadOnlyList<string> _requiredLogicalPaths = requiredLogicalPaths ?? [];

    public async Task MaterializeAsync(
        IArtifactStore store,
        Repository repository,
        string archiveDirectory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string databasePath = ResolveDatabase(repository);
        if (!File.Exists(databasePath))
        {
            if (_requiredLogicalPaths.Count > 0)
            {
                throw new CompletionCertificationException("SQLite workspace database is missing; cannot materialize completed epic archive records.");
            }

            return;
        }

        await using SqliteConnection connection = OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        IReadOnlyList<ArchiveRecord> records =
        [
            ..await ReadLoopHistoriesAsync(connection, cancellationToken),
            ..await ReadExecutionEvidenceAsync(connection, cancellationToken),
        ];
        IReadOnlySet<string> associatedPaths = await ReadAssociatedLogicalPathsAsync(
            connection,
            store,
            repository,
            cancellationToken);
        var byPath = records.ToDictionary(record => record.LogicalPath, StringComparer.Ordinal);
        foreach (string required in _requiredLogicalPaths)
        {
            string normalized = Normalize(required);
            if (associatedPaths is SortedSet<string> mutableAssociatedPaths)
            {
                mutableAssociatedPaths.Add(normalized);
            }

            if (!byPath.ContainsKey(normalized))
            {
                throw new CompletionCertificationException($"Required migrated archive record is missing: {normalized}");
            }
        }

        var artifacts = new CompletionArtifacts(store, repository);
        IReadOnlyList<ArchiveRecord> selectedRecords = associatedPaths.Count == 0
            ? records
            : records.Where(record => associatedPaths.Contains(record.LogicalPath)).ToArray();
        IReadOnlyList<ArchiveRecord> orderedRecords = selectedRecords
            .OrderBy(record => record.TargetPath, StringComparer.Ordinal)
            .ToArray();
        foreach (string duplicateTarget in orderedRecords
            .GroupBy(record => record.TargetPath, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            throw new CompletionCertificationException($"Completed epic archive materialization target planned more than once: {archiveDirectory}/{duplicateTarget}");
        }

        foreach (ArchiveRecord record in orderedRecords)
        {
            string target = $"{archiveDirectory}/{record.TargetPath}";
            if (await artifacts.ExistsAsync(target))
            {
                throw new CompletionCertificationException($"Completed epic archive materialization collision: {target}");
            }

        }

        string metadataPath = $"{archiveDirectory}/archive-metadata.json";
        if (orderedRecords.Count > 0 && await artifacts.ExistsAsync(metadataPath))
        {
            throw new CompletionCertificationException($"Completed epic archive metadata collision: {metadataPath}");
        }

        foreach (ArchiveRecord record in orderedRecords)
        {
            await artifacts.WriteAsync($"{archiveDirectory}/{record.TargetPath}", record.Body);
        }

        if (orderedRecords.Count > 0)
        {
            await artifacts.WriteAsync(
                metadataPath,
                JsonSerializer.Serialize(
                    new ArchiveMetadataDocument(
                        "completed-epic-archive.v1",
                        orderedRecords.Select(record => new ArchiveMetadataRecord(
                            record.Domain,
                            record.LogicalPath,
                            $"{archiveDirectory}/{record.TargetPath}",
                            Sha256(record.Body))).ToArray()),
                    new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        }
    }

    private static async Task<IReadOnlyList<ArchiveRecord>> ReadLoopHistoriesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "loop_history", cancellationToken))
        {
            return [];
        }

        var records = new List<ArchiveRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT logical_path, body, content_hash
            FROM loop_history
            ORDER BY kind, sequence;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string logicalPath = Normalize(reader.GetString(0));
            string body = reader.GetString(1);
            ValidateHash(logicalPath, body, reader.GetString(2));
            if (TryMapLoopHistoryTarget(logicalPath, out string targetPath))
            {
                records.Add(new ArchiveRecord("loop_history", logicalPath, targetPath, body));
            }
        }

        return records;
    }

    private static async Task<IReadOnlySet<string>> ReadAssociatedLogicalPathsAsync(
        SqliteConnection connection,
        IArtifactStore store,
        Repository repository,
        CancellationToken cancellationToken)
    {
        var associated = new SortedSet<string>(StringComparer.Ordinal);
        if (await TableExistsAsync(connection, "roadmap_state", cancellationToken))
        {
            foreach (string document in await ReadTextColumnAsync(
                connection,
                "SELECT document_json FROM roadmap_state WHERE id = 1;",
                cancellationToken))
            {
                AddMigratedPaths(document, associated);
            }
        }

        if (await TableExistsAsync(connection, "transition_journal", cancellationToken))
        {
            foreach (string document in await ReadTextColumnAsync(
                connection,
                """
                SELECT output_paths_json || ' ' || input_hashes_json || ' ' || COALESCE(input_snapshot_json, '')
                FROM transition_journal
                ORDER BY event_order;
                """,
                cancellationToken))
            {
                AddMigratedPaths(document, associated);
            }
        }

        var artifacts = new CompletionArtifacts(store, repository);
        string? completionContext = await artifacts.ReadAsync(
            CompletionArtifactPaths.RoadmapCompletionContext);
        if (!string.IsNullOrWhiteSpace(completionContext))
        {
            AddMigratedPaths(completionContext, associated);
        }

        return associated;
    }

    private static async Task<IReadOnlyList<string>> ReadTextColumnAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        var values = new List<string>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                values.Add(reader.GetString(0));
            }
        }

        return values;
    }

    private static void AddMigratedPaths(string text, SortedSet<string> associated)
    {
        foreach (Match match in AgentsPathRegex().Matches(text))
        {
            string path = Normalize(match.Groups["path"].Value)
                .TrimEnd('.', ',', ';', ':', ')', ']');
            if (IsMigratedArchivePath(path))
            {
                associated.Add(path);
            }
        }
    }

    private static async Task<IReadOnlyList<ArchiveRecord>> ReadExecutionEvidenceAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "execution_evidence", cancellationToken))
        {
            return [];
        }

        var records = new List<ArchiveRecord>();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT logical_path, body, content_hash
            FROM execution_evidence
            ORDER BY stem, sequence;
            """;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            string logicalPath = Normalize(reader.GetString(0));
            string body = reader.GetString(1);
            ValidateHash(logicalPath, body, reader.GetString(2));
            string target = $"evidence/execution/{Path.GetFileName(logicalPath)}";
            records.Add(new ArchiveRecord("execution_evidence", logicalPath, target, body));
        }

        return records;
    }

    private static bool TryMapLoopHistoryTarget(string logicalPath, out string targetPath)
    {
        if (logicalPath.StartsWith(CompletionArtifactPaths.DecisionsDirectory + "/", StringComparison.Ordinal))
        {
            targetPath = $"decisions/{Path.GetFileName(logicalPath)}";
            return true;
        }

        if (logicalPath.StartsWith(CompletionArtifactPaths.DeltasDirectory + "/", StringComparison.Ordinal))
        {
            targetPath = $"deltas/{Path.GetFileName(logicalPath)}";
            return true;
        }

        if (logicalPath.StartsWith(CompletionArtifactPaths.HandoffsDirectory + "/", StringComparison.Ordinal))
        {
            targetPath = $"handoffs/{Path.GetFileName(logicalPath)}";
            return true;
        }

        targetPath = string.Empty;
        return false;
    }

    private static bool IsMigratedArchivePath(string logicalPath) =>
        logicalPath.StartsWith(CompletionArtifactPaths.DecisionsDirectory + "/", StringComparison.Ordinal) ||
        logicalPath.StartsWith(CompletionArtifactPaths.DeltasDirectory + "/", StringComparison.Ordinal) ||
        logicalPath.StartsWith(CompletionArtifactPaths.HandoffsDirectory + "/", StringComparison.Ordinal) ||
        logicalPath.StartsWith(CompletionArtifactPaths.ExecutionEvidenceDirectory + "/", StringComparison.Ordinal);

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

    private static void ValidateHash(string logicalPath, string body, string storedHash)
    {
        if (!string.Equals(storedHash, Sha256(body), StringComparison.Ordinal))
        {
            throw new CompletionCertificationException($"Migrated archive record hash mismatch: {logicalPath}");
        }
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
        path.Replace('\\', '/').TrimStart('/');

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    [GeneratedRegex(@"(?<path>\.agents/[A-Za-z0-9._/\-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex AgentsPathRegex();

    private sealed record ArchiveRecord(
        string Domain,
        string LogicalPath,
        string TargetPath,
        string Body);

    private sealed record ArchiveMetadataDocument(
        string SchemaVersion,
        IReadOnlyList<ArchiveMetadataRecord> Records);

    private sealed record ArchiveMetadataRecord(
        string Domain,
        string LogicalPath,
        string ExportPath,
        string ContentHash);
}
