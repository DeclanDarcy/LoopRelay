using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Storage;

public sealed class CanonicalStorageOperationStore(Repository _repository) : IWorkspaceStorageOperationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task PersistPlanAsync(StorageOperationPlan plan, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO canonical_storage_operation_plans (
                    operation_id, operation_kind, workspace_id, run_id, workflow_instance_id,
                    transition_run_id, attempt_id, source_fingerprint, target_manifest,
                    preconditions_json, postconditions_json, semantic_idempotency_key,
                    current_lifecycle, planned_at
                ) VALUES ($operation, $kind, $workspace, $run, $workflow, $transition, $attempt,
                    $source, $target, $preconditions, $postconditions, $idempotency, 'Planned', $planned)
                ON CONFLICT(semantic_idempotency_key) DO NOTHING;
                """;
            Add(command,
                ("$operation", plan.Identity.Value), ("$kind", plan.Kind.ToString()),
                ("$workspace", plan.Causality.Workspace.Value), ("$run", plan.Causality.Run.Value),
                ("$workflow", plan.Causality.WorkflowInstance.Value),
                ("$transition", plan.Causality.TransitionRun.Value), ("$attempt", plan.Causality.Attempt.Value),
                ("$source", plan.SourceFingerprint), ("$target", plan.TargetManifest),
                ("$preconditions", Serialize(plan.Preconditions)), ("$postconditions", Serialize(plan.Postconditions)),
                ("$idempotency", plan.SemanticIdempotencyKey), ("$planned", Format(plan.PlannedAt)));
            int inserted = await command.ExecuteNonQueryAsync(cancellationToken);
            if (inserted == 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return;
            }
        }
        await InsertEventAsync(connection, transaction, new StorageOperationEvent(
            plan.Identity, StorageOperationLifecycle.Planned, "Storage operation plan persisted before mutation.",
            plan.Preconditions, plan.PlannedAt), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AppendEventAsync(StorageOperationEvent storageEvent, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await InsertEventAsync(connection, transaction, storageEvent, cancellationToken);
        await using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE canonical_storage_operation_plans SET current_lifecycle = $state WHERE operation_id = $operation;";
        Add(update, ("$state", storageEvent.Lifecycle.ToString()), ("$operation", storageEvent.Operation.Value));
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new KeyNotFoundException($"Storage operation `{storageEvent.Operation}` was not found.");
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task PersistReceiptAsync(StorageOperationReceipt receipt, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO canonical_storage_operation_receipts (
                operation_id, observed_fingerprint, effect_receipts_json, evidence_json, recorded_at
            ) VALUES ($operation, $fingerprint, $receipts, $evidence, $recorded)
            ON CONFLICT(operation_id) DO NOTHING;
            """;
        Add(command, ("$operation", receipt.Operation.Value), ("$fingerprint", receipt.ObservedFingerprint),
            ("$receipts", JsonSerializer.Serialize(receipt.EffectReceipts, JsonOptions)),
            ("$evidence", Serialize(receipt.Evidence)), ("$recorded", Format(receipt.RecordedAt)));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await InsertEventAsync(connection, transaction, new StorageOperationEvent(
            receipt.Operation, StorageOperationLifecycle.Completed,
            "Storage operation postcondition and receipt were recorded.", receipt.Evidence, receipt.RecordedAt),
            cancellationToken);
        await using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE canonical_storage_operation_plans SET current_lifecycle = 'Completed' WHERE operation_id = $operation;";
        update.Parameters.AddWithValue("$operation", receipt.Operation.Value);
        await update.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StorageOperationPlan>> ReadInterruptedAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT operation_id, operation_kind, workspace_id, run_id, workflow_instance_id,
                   transition_run_id, attempt_id, source_fingerprint, target_manifest,
                   preconditions_json, postconditions_json, semantic_idempotency_key, planned_at
            FROM canonical_storage_operation_plans
            WHERE current_lifecycle NOT IN ('Completed', 'Refused') ORDER BY planned_at, operation_id;
            """;
        var result = new List<StorageOperationPlan>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new StorageOperationPlan(
                new StorageOperationIdentity(reader.GetString(0)), Parse<StorageOperationKind>(reader.GetString(1)),
                new CanonicalCausalContext(new WorkspaceIdentity(reader.GetString(2)), new RunIdentity(reader.GetString(3)),
                    new WorkflowInstanceIdentity(reader.GetString(4)), new TransitionRunIdentity(reader.GetString(5)),
                    new AttemptIdentity(reader.GetString(6))), reader.GetString(7), reader.GetString(8),
                DeserializeList(reader.GetString(9)), DeserializeList(reader.GetString(10)), reader.GetString(11),
                DateTimeOffset.Parse(reader.GetString(12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        }
        return result;
    }

    private static async Task InsertEventAsync(SqliteConnection connection, SqliteTransaction transaction,
        StorageOperationEvent storageEvent, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO canonical_storage_operation_events (
                operation_id, lifecycle, explanation, evidence_json, recorded_at
            ) VALUES ($operation, $lifecycle, $explanation, $evidence, $recorded);
            """;
        Add(command, ("$operation", storageEvent.Operation.Value), ("$lifecycle", storageEvent.Lifecycle.ToString()),
            ("$explanation", storageEvent.Explanation), ("$evidence", Serialize(storageEvent.Evidence)),
            ("$recorded", Format(storageEvent.RecordedAt)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        string path = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SqliteConnection connection = WorkspaceDatabaseConnectionFactory.OpenMigrationTarget(path);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        return connection;
    }

    private static void Add(SqliteCommand command, params (string Name, object? Value)[] values)
    {
        foreach ((string name, object? value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
    private static string Serialize(IReadOnlyList<string> values) => JsonSerializer.Serialize(values, JsonOptions);
    private static IReadOnlyList<string> DeserializeList(string json) => JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
    private static T Parse<T>(string value) where T : struct, Enum => Enum.Parse<T>(value, ignoreCase: false);
    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
}
