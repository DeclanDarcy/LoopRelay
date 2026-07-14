using System.Text.RegularExpressions;
using LoopRelay.Completion.Models.Certification;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Infrastructure.Services.Artifacts;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Completion.Services.ArtifactStorage;

public sealed partial class CompletionArtifacts(
    IArtifactStore _store,
    Repository _repository,
    IExecutionEvidenceStore? executionEvidenceStore = null)
{
    private readonly IArtifactStore artifacts = _store is RepositoryArtifactStore
        ? _store
        : new RepositoryArtifactStore(_store, _repository);
    private readonly IExecutionEvidenceStore _executionEvidenceStore =
        executionEvidenceStore ?? new FileBackedExecutionEvidenceStore(
            _store is RepositoryArtifactStore ? _store : new RepositoryArtifactStore(_store, _repository));

    public Repository Repository => _repository;

    public IExecutionEvidenceStore ExecutionEvidenceStore => _executionEvidenceStore;

    public Task<bool> ExistsAsync(string relativePath) => artifacts.ExistsAsync(relativePath);

    public Task<string?> ReadAsync(string relativePath) => artifacts.ReadAsync(relativePath);

    public Task WriteAsync(string relativePath, string content) => artifacts.WriteAsync(relativePath, content);

    public Task DeleteAsync(string relativePath) => artifacts.DeleteAsync(relativePath);

    public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
    {
        if (IsExecutionEvidenceDirectory(relativeDirectory) &&
            _executionEvidenceStore is SqliteExecutionEvidenceStore)
        {
            return (await _executionEvidenceStore.ListAsync(searchPattern))
                .Select(record => record.RelativePath)
                .ToArray();
        }

        IReadOnlyList<string> filesystemPaths = await artifacts.ListAsync(relativeDirectory, searchPattern);

        if (TryLoopHistoryKind(relativeDirectory, out string? kind) &&
            kind is not null &&
            await ListSqliteLoopHistoryAsync(kind, searchPattern) is { } sqliteLoopHistory)
        {
            return sqliteLoopHistory
                .Concat(filesystemPaths)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        return filesystemPaths;
    }

    public async Task<IReadOnlyList<string>> ListDirectoriesAsync(string relativeDirectory)
    {
        return await artifacts.ListDirectoriesAsync(relativeDirectory);
    }

    public async Task<string> ReadRequiredAsync(string relativePath)
    {
        string? content = await ReadAsync(relativePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new CompletionCertificationException($"Required artifact is missing or empty: {relativePath}");
        }

        return content;
    }

    public async Task<string> WriteNumberedEvidenceAsync(string evidenceDirectory, string stem, string content)
    {
        if (IsExecutionEvidenceDirectory(evidenceDirectory))
        {
            return (await _executionEvidenceStore.WriteAsync(stem, content)).RelativePath;
        }

        string path = await NextNumberedPathAsync(evidenceDirectory, stem);
        await WriteAsync(path, content);
        return path;
    }

    public async Task<string> NextNumberedPathAsync(string evidenceDirectory, string stem)
    {
        if (IsExecutionEvidenceDirectory(evidenceDirectory))
        {
            return await _executionEvidenceStore.NextPathAsync(stem);
        }

        IReadOnlyList<string> existing = await ListAsync(evidenceDirectory, $"{stem}.*.md");
        int max = 0;
        foreach (string path in existing)
        {
            Match match = NumberedEvidenceRegex().Match(Path.GetFileName(path));
            if (match.Success && int.TryParse(match.Groups["number"].Value, out int number))
            {
                max = Math.Max(max, number);
            }
        }

        return $"{evidenceDirectory}/{stem}.{max + 1:0000}.md";
    }

    public async Task MoveFileIfPresentAsync(string sourceRelativePath, string targetRelativePath)
    {
        string? content = await ReadAsync(sourceRelativePath);
        if (content is null)
        {
            return;
        }

        if (await ExistsAsync(targetRelativePath))
        {
            throw new CompletionCertificationException($"Archive target already exists: {targetRelativePath}");
        }

        await WriteAsync(targetRelativePath, content);
        await DeleteAsync(sourceRelativePath);
    }

    public async Task CopyFileIfPresentAsync(string sourceRelativePath, string targetRelativePath)
    {
        string? content = await ReadAsync(sourceRelativePath);
        if (content is null)
        {
            return;
        }

        if (await ExistsAsync(targetRelativePath))
        {
            throw new CompletionCertificationException($"Archive target already exists: {targetRelativePath}");
        }

        await WriteAsync(targetRelativePath, content);
    }

    public async Task MoveDirectoryContentsAsync(string sourceDirectory, string targetDirectory)
    {
        IReadOnlyList<string> files = await ListAsync(sourceDirectory, "*");
        foreach (string sourcePath in files.Order(StringComparer.Ordinal))
        {
            string relativeSuffix = RelativeSuffix(sourceDirectory, sourcePath);
            await MoveFileIfPresentAsync(sourcePath, Join(targetDirectory, relativeSuffix));
        }
    }

    private static bool IsExecutionEvidenceDirectory(string evidenceDirectory) =>
        string.Equals(
            evidenceDirectory.Replace('\\', '/').TrimEnd('/'),
            FileBackedExecutionEvidenceStore.ExecutionEvidenceDirectory,
            StringComparison.Ordinal);

    private async Task<IReadOnlyList<string>?> ListSqliteLoopHistoryAsync(string kind, string searchPattern)
    {
        string databasePath = Path.Combine(
            Path.GetFullPath(_repository.Path),
            ".LoopRelay",
            "persistence",
            "looprelay.sqlite3");
        if (!File.Exists(databasePath))
        {
            return null;
        }

        await using SqliteConnection connection = new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        await connection.OpenAsync();
        if (!await TableExistsAsync(connection, "loop_history"))
        {
            return null;
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT logical_path
            FROM loop_history
            WHERE kind = $kind
            ORDER BY sequence;
            """;
        command.Parameters.AddWithValue("$kind", kind);
        var paths = new List<string>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string path = reader.GetString(0);
            if (FileNameMatches(Path.GetFileName(path), searchPattern))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $table;";
        command.Parameters.AddWithValue("$table", table);
        object? scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt64(scalar) == 1;
    }

    private static bool TryLoopHistoryKind(string relativeDirectory, out string? kind)
    {
        string normalized = relativeDirectory.Replace('\\', '/').TrimEnd('/');
        kind = normalized switch
        {
            CompletionArtifactPaths.DecisionsDirectory => "Decisions",
            CompletionArtifactPaths.HandoffsDirectory => "Handoff",
            CompletionArtifactPaths.DeltasDirectory => "OperationalDelta",
            _ => null,
        };
        return kind is not null;
    }

    private static string RelativeSuffix(string sourceDirectory, string sourcePath)
    {
        string normalizedDirectory = Normalize(sourceDirectory).TrimEnd('/');
        string normalizedPath = Normalize(sourcePath);
        if (!normalizedPath.StartsWith(normalizedDirectory + "/", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileName(normalizedPath);
        }

        return normalizedPath[(normalizedDirectory.Length + 1)..];
    }

    private static string Join(string left, string right) =>
        $"{Normalize(left).TrimEnd('/')}/{Normalize(right).TrimStart('/')}";

    private static string Normalize(string path) =>
        path.Replace('\\', '/');

    private static bool FileNameMatches(string fileName, string searchPattern)
    {
        if (searchPattern == "*")
        {
            return true;
        }

        int star = searchPattern.IndexOf('*', StringComparison.Ordinal);
        if (star < 0)
        {
            return string.Equals(fileName, searchPattern, StringComparison.OrdinalIgnoreCase);
        }

        string prefix = searchPattern[..star];
        string suffix = searchPattern[(star + 1)..];
        return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
            fileName.Length >= prefix.Length + suffix.Length;
    }

    [GeneratedRegex(@"\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedEvidenceRegex();
}
