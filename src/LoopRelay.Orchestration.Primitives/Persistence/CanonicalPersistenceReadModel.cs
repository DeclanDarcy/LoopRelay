using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Persistence;

public sealed record CanonicalUnsettledEffectProjection(
    string Identity,
    string State,
    IReadOnlyList<string> Evidence,
    string AttemptIdentity,
    bool Required);

public sealed record CanonicalPersistenceReadModel(
    string ProjectionIdentity,
    CanonicalWorkflowPersistenceSnapshot Workflow,
    IReadOnlyList<CanonicalChainBoundaryEventRecord> ChainBoundaries,
    IReadOnlyList<string> CertifiedTerminalAttempts,
    IReadOnlyList<string> UnsettledRequiredEffectAttempts,
    IReadOnlyList<CanonicalUnsettledEffectProjection> UnsettledEffects)
{
    public static CanonicalPersistenceReadModel Empty { get; } = new(
        "canonical-persistence-read-model.v1",
        new CanonicalWorkflowPersistenceSnapshot([], [], [], [], [], [], [], [], []),
        [], [], [], []);
}

/// <summary>
/// Persistence-owned projection boundary. Consumers receive a stable read model and never query
/// ledger tables or choose storage implementations.
/// </summary>
public interface ICanonicalPersistenceProjection
{
    Task<CanonicalPersistenceReadModel> ProjectAsync(
        Repository repository,
        CancellationToken cancellationToken = default);
}

public sealed class CanonicalPersistenceProjection : ICanonicalPersistenceProjection
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<CanonicalPersistenceReadModel> ProjectAsync(
        Repository repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        IReadOnlyList<(EffectIntent Intent, string State)> unsettled =
            await ReadUnsettledEffectsAsync(repository, cancellationToken);
        IReadOnlyList<string> unsettledRequiredAttempts = unsettled
            .Where(item => item.Intent.Requiredness is EffectRequiredness.BlockingLocal or
                EffectRequiredness.RequiredAsync)
            .Select(item => item.Intent.Causality.Attempt.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new CanonicalPersistenceReadModel(
            "canonical-persistence-read-model.v1",
            await store.LoadSnapshotAsync(cancellationToken),
            await store.ReadChainBoundaryEventsAsync(cancellationToken),
            await store.ReadCertifiedTerminalAttemptIdentitiesAsync(cancellationToken),
            unsettledRequiredAttempts,
            unsettled.Select(item => new CanonicalUnsettledEffectProjection(
                item.Intent.Identity.Value,
                item.State,
                [$"effect:{item.Intent.Identity.Value}:{item.State}"],
                item.Intent.Causality.Attempt.Value,
                item.Intent.Requiredness is EffectRequiredness.BlockingLocal or
                    EffectRequiredness.RequiredAsync)).ToArray());
    }

    private static async Task<IReadOnlyList<(EffectIntent Intent, string State)>> ReadUnsettledEffectsAsync(
        Repository repository,
        CancellationToken cancellationToken)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(repository);
        if (!File.Exists(database)) return [];
        try
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
            await connection.OpenAsync(cancellationToken);
            await using (SqliteCommand exists = connection.CreateCommand())
            {
                exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='canonical_effect_intents';";
                if (Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken)) != 1) return [];
            }
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT definition_json,status FROM canonical_effect_intents
                WHERE terminal_receipt_id IS NULL
                  AND status IN ('Planned','Pending','Started','Unknown','Reconciling','RetryAuthorized','Leased')
                ORDER BY effect_order,planned_at,effect_intent_id LIMIT 1024;
                """;
            var result = new List<(EffectIntent, string)>();
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                EffectIntent? intent = JsonSerializer.Deserialize<EffectIntent>(reader.GetString(0), JsonOptions);
                if (intent is not null) result.Add((intent, reader.GetString(1)));
            }
            return result;
        }
        catch (SqliteException)
        {
            return [];
        }
    }
}
