using System.Globalization;
using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Infrastructure.Services.Effects;

public sealed class WorkspaceCheckpointCleanupEffectExecutor(Repository _repository) : IEffectExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public EffectExecutorKey Key => WorkspaceEffectExecutorKeys.CheckpointCleanup;
    public string Version => "1";

    public async Task<EffectExecutionObservation> ExecuteAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        WorkspaceCheckpointCleanupPayload payload = Parse(intent);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        long before = await CountAsync(connection, payload.MetadataKeys, cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        foreach (string key in payload.MetadataKeys.Distinct(StringComparer.Ordinal))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM workspace_metadata WHERE key = $key;";
            command.Parameters.AddWithValue("$key", key);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        long after = await CountAsync(connection, payload.MetadataKeys, cancellationToken);
        bool satisfied = after == 0;
        return new EffectExecutionObservation(
            satisfied ? EffectLifecycle.Succeeded : EffectLifecycle.Failed,
            satisfied ? "Completion checkpoints were retired." : "Completion checkpoints remain after cleanup.",
            payload.MetadataKeys,
            before.ToString(CultureInfo.InvariantCulture),
            after.ToString(CultureInfo.InvariantCulture),
            satisfied,
            "workspace_metadata");
    }

    internal static WorkspaceCheckpointCleanupPayload Parse(EffectIntent intent) =>
        JsonSerializer.Deserialize<WorkspaceCheckpointCleanupPayload>(intent.TypedPayload, JsonOptions)
        ?? throw new InvalidOperationException("Workspace checkpoint cleanup payload is invalid.");

    internal async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        string database = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(database)!);
        var connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(database);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        return connection;
    }

    internal static async Task<long> CountAsync(
        SqliteConnection connection,
        IReadOnlyList<string> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0) return 0;
        await using SqliteCommand command = connection.CreateCommand();
        string[] parameters = keys.Distinct(StringComparer.Ordinal)
            .Select((_, index) => $"$key{index}")
            .ToArray();
        command.CommandText = $"SELECT COUNT(*) FROM workspace_metadata WHERE key IN ({string.Join(',', parameters)});";
        for (int index = 0; index < parameters.Length; index++)
        {
            command.Parameters.AddWithValue(parameters[index], keys.Distinct(StringComparer.Ordinal).ElementAt(index));
        }
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }
}

public sealed class WorkspaceCheckpointCleanupReconciler(Repository repository) : IEffectReconciler
{
    private readonly WorkspaceCheckpointCleanupEffectExecutor _observer = new(repository);

    public async Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        WorkspaceCheckpointCleanupPayload payload = WorkspaceCheckpointCleanupEffectExecutor.Parse(intent);
        await using SqliteConnection connection = await _observer.OpenAsync(cancellationToken);
        long remaining = await WorkspaceCheckpointCleanupEffectExecutor.CountAsync(
            connection, payload.MetadataKeys, cancellationToken);
        return remaining == 0
            ? new EffectReconciliationObservation(
                EffectReconciliationVerdict.Succeeded,
                "Workspace metadata independently confirms checkpoint retirement.",
                payload.MetadataKeys,
                "unknown",
                "0",
                "workspace_metadata")
            : new EffectReconciliationObservation(
                EffectReconciliationVerdict.NotApplied,
                "Completion checkpoints are still present.",
                payload.MetadataKeys,
                "unknown",
                remaining.ToString(CultureInfo.InvariantCulture));
    }
}
