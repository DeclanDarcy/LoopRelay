using System.Text.Json;
using CommandCenter.Core.Repositories;
using CommandCenter.Persistence.Sqlite.Abstractions;
using Dapper;
using Microsoft.Data.Sqlite;

namespace CommandCenter.Persistence.Sqlite;

/// <summary>
/// Dapper-backed <see cref="IDerivedSnapshotCache"/>. Stores the source-pure base as a
/// <c>payload_json</c> text column round-tripped through a single <see cref="JsonSerializerOptions"/>,
/// so the wire shape is preserved by construction when a caller hands back the exact serializer its
/// DTOs use. A read is a single keyed lookup that additionally filters on
/// <c>source_fingerprint</c> + <c>formula_version</c>, so a stale fingerprint or a bumped formula
/// version returns zero rows (a miss) rather than a stale base; a write is an atomic UPSERT on the
/// <c>(repository_id, kind)</c> primary key.
/// </summary>
public sealed class SqliteDerivedSnapshotCache : IDerivedSnapshotCache
{
    private static readonly JsonSerializerOptions DefaultPayloadOptions =
        new(JsonSerializerDefaults.Web);

    private const string SchemaVersionTag = "v1";

    private readonly ISqliteConnectionFactory connectionFactory;
    private readonly IRepositoryLocator repositoryLocator;
    private readonly TimeProvider timeProvider;
    private readonly JsonSerializerOptions payloadOptions;

    public SqliteDerivedSnapshotCache(
        ISqliteConnectionFactory connectionFactory,
        IRepositoryLocator repositoryLocator,
        TimeProvider timeProvider,
        JsonSerializerOptions? payloadOptions = null)
    {
        this.connectionFactory = connectionFactory;
        this.repositoryLocator = repositoryLocator;
        this.timeProvider = timeProvider;
        this.payloadOptions = payloadOptions ?? DefaultPayloadOptions;
    }

    public async Task<T?> TryGetAsync<T>(
        Guid repo, string kind, string fingerprint, string formulaVersion, CancellationToken ct)
    {
        Repository repository = await ResolveAsync(repo, ct).ConfigureAwait(false);
        await using SqliteConnection connection =
            await connectionFactory.OpenRepositoryConnectionAsync(repository, ct).ConfigureAwait(false);

        var command = new CommandDefinition(
            """
            SELECT payload_json
            FROM derived_snapshot
            WHERE repository_id = @RepositoryId
              AND kind = @Kind
              AND source_fingerprint = @Fingerprint
              AND formula_version = @FormulaVersion;
            """,
            new
            {
                RepositoryId = repo.ToString(),
                Kind = kind,
                Fingerprint = fingerprint,
                FormulaVersion = formulaVersion
            },
            cancellationToken: ct);

        string? payloadJson = await connection.ExecuteScalarAsync<string?>(command).ConfigureAwait(false);
        if (payloadJson is null)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(payloadJson, payloadOptions);
    }

    public async Task PutAsync<T>(
        Guid repo, string kind, string fingerprint, string formulaVersion, T baseValue, CancellationToken ct)
    {
        Repository repository = await ResolveAsync(repo, ct).ConfigureAwait(false);
        await using SqliteConnection connection =
            await connectionFactory.OpenRepositoryConnectionAsync(repository, ct).ConfigureAwait(false);

        string payloadJson = JsonSerializer.Serialize(baseValue, payloadOptions);

        var command = new CommandDefinition(
            """
            INSERT INTO derived_snapshot
                (repository_id, kind, source_fingerprint, formula_version, schema_version, computed_at, payload_json)
            VALUES
                (@RepositoryId, @Kind, @Fingerprint, @FormulaVersion, @SchemaVersion, @ComputedAt, @PayloadJson)
            ON CONFLICT (repository_id, kind) DO UPDATE SET
                source_fingerprint = excluded.source_fingerprint,
                formula_version    = excluded.formula_version,
                schema_version     = excluded.schema_version,
                computed_at        = excluded.computed_at,
                payload_json       = excluded.payload_json;
            """,
            new
            {
                RepositoryId = repo.ToString(),
                Kind = kind,
                Fingerprint = fingerprint,
                FormulaVersion = formulaVersion,
                SchemaVersion = SchemaVersionTag,
                ComputedAt = timeProvider.GetUtcNow().ToString("O"),
                PayloadJson = payloadJson
            },
            cancellationToken: ct);

        await connection.ExecuteAsync(command).ConfigureAwait(false);
    }

    private async Task<Repository> ResolveAsync(Guid repo, CancellationToken ct)
    {
        Repository? repository = await repositoryLocator.FindAsync(repo, ct).ConfigureAwait(false);
        if (repository is null)
        {
            throw new InvalidOperationException(
                $"Cannot open the derived-cache database for unknown repository '{repo}'.");
        }

        return repository;
    }
}
