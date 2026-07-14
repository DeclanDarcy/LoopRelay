using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Storage;

public sealed record CanonicalExportValue(string Kind, string? Value);
public sealed record CanonicalExportRow(IReadOnlyList<CanonicalExportValue> Values);
public sealed record CanonicalExportDomain(
    string Name,
    IReadOnlyList<string> Columns,
    string OrderingRule,
    IReadOnlyList<CanonicalExportRow> Rows,
    string Sha256);
public sealed record CanonicalStorageExportManifest(
    string Codec,
    int CodecVersion,
    int SchemaVersion,
    string WorkspaceIdentity,
    IReadOnlyDictionary<string, int> DomainRowCounts,
    IReadOnlyDictionary<string, string> DomainHashes,
    string LogicalFingerprint,
    string PackageSha256);
public sealed record CanonicalStorageExportPackage(
    CanonicalStorageExportManifest Manifest,
    IReadOnlyList<CanonicalExportDomain> Domains);
public sealed record CanonicalSemanticDiff(string Domain, string ExpectedHash, string ActualHash, int ExpectedRows, int ActualRows);

public sealed class CanonicalStorageExportCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CanonicalStorageExportPackage> ExportAsync(
        string databasePath,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
        WorkspaceSchemaInspection inspection = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection, cancellationToken);
        if (inspection.Family != WorkspaceSchemaFamily.CanonicalWorkspace ||
            inspection.Version != LoopRelayWorkspaceDatabase.CurrentSchemaVersion)
            throw new InvalidOperationException("Semantic export requires a healthy current canonical authority.");
        string workspace = await ScalarAsync(connection,
            "SELECT workspace_id FROM workspace_identity WHERE id = 1;", cancellationToken)
            ?? throw new InvalidDataException("Canonical export has no workspace identity.");
        string[] tables = await TablesAsync(connection, cancellationToken);
        var domains = new List<CanonicalExportDomain>(tables.Length);
        foreach (string table in tables)
            domains.Add(await ReadDomainAsync(connection, table, cancellationToken));
        domains.Sort((left, right) => StringComparer.Ordinal.Compare(left.Name, right.Name));
        string logical = Hash(string.Join("\n", domains.Select(domain =>
            $"{domain.Name}:{domain.Rows.Count}:{domain.Sha256}")));
        var counts = domains.ToDictionary(item => item.Name, item => item.Rows.Count, StringComparer.Ordinal);
        var hashes = domains.ToDictionary(item => item.Name, item => item.Sha256, StringComparer.Ordinal);
        var provisional = new CanonicalStorageExportManifest(
            "looprelay-canonical-storage", 1, inspection.Version.Value, workspace, counts, hashes, logical, string.Empty);
        var package = new CanonicalStorageExportPackage(provisional, domains);
        string packageHash = Hash(JsonSerializer.Serialize(package, JsonOptions));
        return package with { Manifest = provisional with { PackageSha256 = packageHash } };
    }

    public string Encode(CanonicalStorageExportPackage package) => JsonSerializer.Serialize(package, JsonOptions);

    public CanonicalStorageExportPackage Decode(string json)
    {
        CanonicalStorageExportPackage package = JsonSerializer.Deserialize<CanonicalStorageExportPackage>(json, JsonOptions)
            ?? throw new InvalidDataException("Canonical storage export package is empty.");
        string expected = package.Manifest.PackageSha256;
        string actual = Hash(JsonSerializer.Serialize(
            package with { Manifest = package.Manifest with { PackageSha256 = string.Empty } }, JsonOptions));
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
            throw new InvalidDataException("Canonical storage export package hash does not match its content.");
        return package;
    }

    public async Task RehydrateFreshAsync(
        CanonicalStorageExportPackage package,
        string targetDatabasePath,
        CancellationToken cancellationToken = default)
    {
        if (File.Exists(targetDatabasePath))
            throw new InvalidOperationException("Canonical rehydration requires a fresh target path.");
        Directory.CreateDirectory(Path.GetDirectoryName(targetDatabasePath)!);
        await using SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(targetDatabasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await ExecuteAsync(connection, transaction, "PRAGMA defer_foreign_keys = ON;", cancellationToken);
        foreach (CanonicalExportDomain domain in package.Domains.Reverse())
            await ExecuteAsync(connection, transaction, $"DELETE FROM {Quote(domain.Name)};", cancellationToken);
        foreach (CanonicalExportDomain domain in package.Domains)
        {
            string columns = string.Join(",", domain.Columns.Select(Quote));
            string parameters = string.Join(",", domain.Columns.Select((_, index) => $"$v{index}"));
            foreach (CanonicalExportRow row in domain.Rows)
            {
                await using SqliteCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = $"INSERT INTO {Quote(domain.Name)} ({columns}) VALUES ({parameters});";
                for (int index = 0; index < row.Values.Count; index++)
                    insert.Parameters.AddWithValue($"$v{index}", DecodeValue(row.Values[index]));
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public static IReadOnlyList<CanonicalSemanticDiff> Compare(
        CanonicalStorageExportPackage expected,
        CanonicalStorageExportPackage actual)
    {
        var left = expected.Domains.ToDictionary(item => item.Name, StringComparer.Ordinal);
        var right = actual.Domains.ToDictionary(item => item.Name, StringComparer.Ordinal);
        return left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(name => (Name: name, Left: left.GetValueOrDefault(name), Right: right.GetValueOrDefault(name)))
            .Where(item => item.Left is null || item.Right is null ||
                           !string.Equals(item.Left.Sha256, item.Right.Sha256, StringComparison.Ordinal))
            .Select(item => new CanonicalSemanticDiff(item.Name, item.Left?.Sha256 ?? "missing",
                item.Right?.Sha256 ?? "missing", item.Left?.Rows.Count ?? 0, item.Right?.Rows.Count ?? 0))
            .ToArray();
    }

    private static async Task<CanonicalExportDomain> ReadDomainAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        var columns = new List<string>();
        await using (SqliteCommand info = connection.CreateCommand())
        {
            info.CommandText = $"PRAGMA table_info({Quote(table)});";
            await using SqliteDataReader reader = await info.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) columns.Add(reader.GetString(1));
        }
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {string.Join(",", columns.Select(Quote))} FROM {Quote(table)};";
        var rows = new List<CanonicalExportRow>();
        await using SqliteDataReader data = await command.ExecuteReaderAsync(cancellationToken);
        while (await data.ReadAsync(cancellationToken))
        {
            var values = new CanonicalExportValue[data.FieldCount];
            for (int index = 0; index < data.FieldCount; index++) values[index] = EncodeValue(data.GetValue(index));
            rows.Add(new CanonicalExportRow(values));
        }
        rows.Sort((left, right) => StringComparer.Ordinal.Compare(
            JsonSerializer.Serialize(left, JsonOptions), JsonSerializer.Serialize(right, JsonOptions)));
        string domainHash = Hash(JsonSerializer.Serialize(new { table, columns, rows }, JsonOptions));
        return new CanonicalExportDomain(table, columns, "canonical-row-json-ordinal", rows, domainHash);
    }

    private static async Task<string[]> TablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        var result = new List<string>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0));
        return result.ToArray();
    }

    private static CanonicalExportValue EncodeValue(object value) => value switch
    {
        DBNull => new("null", null),
        byte[] bytes => new("blob", Convert.ToBase64String(bytes)),
        long number => new("integer", number.ToString(CultureInfo.InvariantCulture)),
        double number => new("real", number.ToString("R", CultureInfo.InvariantCulture)),
        _ => new("text", Convert.ToString(value, CultureInfo.InvariantCulture)),
    };
    private static object DecodeValue(CanonicalExportValue value) => value.Kind switch
    {
        "null" => DBNull.Value,
        "blob" => Convert.FromBase64String(value.Value!),
        "integer" => long.Parse(value.Value!, CultureInfo.InvariantCulture),
        "real" => double.Parse(value.Value!, CultureInfo.InvariantCulture),
        "text" => value.Value ?? string.Empty,
        _ => throw new InvalidDataException($"Unknown canonical export value kind `{value.Kind}`."),
    };
    private static async Task<string?> ScalarAsync(SqliteConnection connection, string sql, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand(); command.CommandText = sql;
        object? value = await command.ExecuteScalarAsync(token);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }
    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken token)
    {
        await using SqliteCommand command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }
    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
