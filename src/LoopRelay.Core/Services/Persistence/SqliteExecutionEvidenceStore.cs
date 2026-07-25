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
        string? likeFilter = TryBuildLikeSuperset(searchPattern);
        command.CommandText = likeFilter is null
            ? """
              SELECT stem, sequence, logical_path, body, content_hash
              FROM execution_evidence
              ORDER BY stem, sequence;
              """
            : """
              SELECT stem, sequence, logical_path, body, content_hash
              FROM execution_evidence
              WHERE logical_path LIKE $like ESCAPE '\'
              ORDER BY stem, sequence;
              """;
        if (likeFilter is not null)
        {
            command.Parameters.AddWithValue("$like", likeFilter);
        }

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

    /// <summary>
    /// Path-only listing (PERF-14): the same rows <see cref="ListAsync"/> would return, without
    /// their bodies. Consumers that only enumerate what evidence exists - rather than read what it
    /// says - pay neither the multi-kilobyte body fetch per row nor the SHA-256 validation of a
    /// document they are about to discard.
    ///
    /// <para>
    /// Hash validation is deliberately absent rather than forgotten: it is a statement about a body,
    /// and no body is read here. <see cref="ListAsync"/> remains the content path and still
    /// validates every row it returns, so nothing weakens for consumers that need the text.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ExecutionEvidencePath>> ListPathsAsync(string searchPattern = "*.md")
    {
        string databasePath = ResolveDatabase(repository);
        await using SqliteConnection connection = OpenReadOnly(databasePath);
        await connection.OpenAsync();
        await using SqliteCommand command = connection.CreateCommand();
        string? likeFilter = TryBuildLikeSuperset(searchPattern);
        command.CommandText = likeFilter is null
            ? """
              SELECT stem, sequence, logical_path
              FROM execution_evidence
              ORDER BY stem, sequence;
              """
            : """
              SELECT stem, sequence, logical_path
              FROM execution_evidence
              WHERE logical_path LIKE $like ESCAPE '\'
              ORDER BY stem, sequence;
              """;
        if (likeFilter is not null)
        {
            command.Parameters.AddWithValue("$like", likeFilter);
        }

        var paths = new List<ExecutionEvidencePath>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            string relativePath = reader.GetString(2);
            if (!GlobMatches(Path.GetFileName(relativePath), searchPattern))
            {
                continue;
            }

            paths.Add(new ExecutionEvidencePath(
                reader.GetString(0),
                reader.GetInt32(1),
                relativePath));
        }

        return paths;
    }

    /// <summary>
    /// Builds a SQL <c>LIKE</c> pattern that matches a strict superset of what
    /// <see cref="GlobMatches"/> accepts, so SQLite can discard obviously-irrelevant rows before
    /// they are materialized while <see cref="GlobMatches"/> stays the sole authority on the
    /// result. Returns <see langword="null"/> when no safe filter can be derived, in which case
    /// every row is read and filtered in memory exactly as before.
    ///
    /// <para>
    /// Superset, never equivalence: a filter that excluded a row <see cref="GlobMatches"/> would
    /// have accepted would silently change results, so the two ways that could happen are both
    /// ruled out below - <c>LIKE</c> metacharacters in the caller's pattern are escaped, and
    /// non-ASCII patterns decline the filter entirely because SQLite's <c>LIKE</c> folds case for
    /// ASCII only while <see cref="StringComparison.OrdinalIgnoreCase"/> folds the full Unicode
    /// range.
    /// </para>
    /// </summary>
    private static string? TryBuildLikeSuperset(string searchPattern)
    {
        if (searchPattern == "*")
        {
            return null;
        }

        int star = searchPattern.IndexOf('*', StringComparison.Ordinal);
        string prefix = star < 0 ? searchPattern : searchPattern[..star];
        string suffix = star < 0 ? string.Empty : searchPattern[(star + 1)..];
        if (!Ascii.IsValid(prefix) || !Ascii.IsValid(suffix))
        {
            return null;
        }

        // A logical path always ends with the file name the glob is matched against, so "file name
        // equals the pattern" implies "path ends with the pattern", and "file name starts with
        // prefix and ends with suffix" implies "path contains prefix somewhere ahead of a trailing
        // suffix" - GlobMatches' own length check rules out the two overlapping.
        return star < 0
            ? "%" + EscapeLike(prefix)
            : "%" + EscapeLike(prefix) + "%" + EscapeLike(suffix);
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

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

    private static string ResolveDatabase(Repository repository) =>
        LoopRelayWorkspaceDatabase.Resolve(repository);

    private static SqliteConnection OpenReadOnly(string databasePath) =>
        LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);

    private static SqliteConnection OpenReadWrite(string databasePath) =>
        LoopRelayWorkspaceDatabase.OpenReadWrite(databasePath);

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
