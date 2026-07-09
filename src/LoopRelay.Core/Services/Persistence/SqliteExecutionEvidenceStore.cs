using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LoopRelay.Core.Abstractions.Persistence;
using LoopRelay.Core.Models.Repositories;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Core.Services.Persistence;

public sealed partial class SqliteExecutionEvidenceStore(Repository repository) : IExecutionEvidenceStore
{
    public const string ExecutionEvidenceDirectory = FileBackedExecutionEvidenceStore.ExecutionEvidenceDirectory;
    private const string RelativeDatabasePath = ".LoopRelay/persistence/looprelay.sqlite3";

    public async Task<ExecutionEvidenceRecord> WriteAsync(string stem, string content)
    {
        string databasePath = ResolveDatabase(repository);
        await using SqliteConnection connection = OpenReadWrite(databasePath);
        await connection.OpenAsync();
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync();
        int sequence = await NextSequenceAsync(connection, transaction, stem);
        string relativePath = EvidencePath(stem, sequence);

        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO execution_evidence (
                logical_path, stem, sequence, body, content_hash, created_at, writer, metadata_json)
            VALUES (
                $logical_path, $stem, $sequence, $body, $content_hash, $created_at, NULL, '{}');
            """,
            ("$logical_path", relativePath),
            ("$stem", stem),
            ("$sequence", sequence),
            ("$body", content),
            ("$content_hash", Sha256(content)),
            ("$created_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        await transaction.CommitAsync();

        return new ExecutionEvidenceRecord(stem, sequence, relativePath, content);
    }

    public async Task<string> NextPathAsync(string stem)
    {
        string databasePath = ResolveDatabase(repository);
        await using SqliteConnection connection = OpenReadOnly(databasePath);
        await connection.OpenAsync();
        int sequence = await NextSequenceAsync(connection, null, stem);
        return EvidencePath(stem, sequence);
    }

    public async Task<ExecutionEvidenceRecord?> ReadAsync(string relativePath)
    {
        string normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
        Match match = ExecutionEvidencePathRegex().Match(normalizedPath);
        if (!match.Success || !int.TryParse(match.Groups["number"].Value, out int sequence) || sequence <= 0)
        {
            return null;
        }

        string stem = match.Groups["stem"].Value;
        string databasePath = ResolveDatabase(repository);
        await using SqliteConnection connection = OpenReadOnly(databasePath);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT body, content_hash
            FROM execution_evidence
            WHERE logical_path = $logical_path AND stem = $stem AND sequence = $sequence;
            """;
        command.Parameters.AddWithValue("$logical_path", normalizedPath);
        command.Parameters.AddWithValue("$stem", stem);
        command.Parameters.AddWithValue("$sequence", sequence);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        string body = reader.GetString(0);
        ValidateHash(normalizedPath, body, reader.GetString(1));
        return new ExecutionEvidenceRecord(stem, sequence, normalizedPath, body);
    }

    public async Task<IReadOnlyList<ExecutionEvidenceRecord>> ListAsync(string searchPattern = "*.md")
    {
        string databasePath = ResolveDatabase(repository);
        await using SqliteConnection connection = OpenReadOnly(databasePath);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT stem, sequence, logical_path, body, content_hash
            FROM execution_evidence
            ORDER BY stem, sequence;
            """;

        var records = new List<ExecutionEvidenceRecord>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string relativePath = reader.GetString(2);
            if (!GlobMatches(Path.GetFileName(relativePath), searchPattern))
            {
                continue;
            }

            string body = reader.GetString(3);
            ValidateHash(relativePath, body, reader.GetString(4));
            records.Add(new ExecutionEvidenceRecord(
                reader.GetString(0),
                reader.GetInt32(1),
                relativePath,
                body));
        }

        return records;
    }

    private static async Task<int> NextSequenceAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string stem)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT COALESCE(MAX(sequence), 0) FROM execution_evidence WHERE stem = $stem;";
        command.Parameters.AddWithValue("$stem", stem);
        object? scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt32(scalar, CultureInfo.InvariantCulture) + 1;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string commandText,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static void ValidateHash(string relativePath, string body, string storedHash)
    {
        if (!string.Equals(storedHash, Sha256(body), StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Execution evidence hash mismatch for `{relativePath}`.");
        }
    }

    private static string EvidencePath(string stem, int sequence) =>
        $"{ExecutionEvidenceDirectory}/{stem}.{sequence:0000}.md";

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

    private static SqliteConnection OpenReadWrite(string databasePath) =>
        new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static bool GlobMatches(string fileName, string searchPattern)
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

    [GeneratedRegex(@"^\.agents/evidence/execution/(?<stem>.+)\.(?<number>\d{4})\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex ExecutionEvidencePathRegex();
}
