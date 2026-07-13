using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Persistence;

public sealed class CanonicalEffectWorkStore(Repository _repository) : IEffectWorkStore, IEffectPlanStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task AppendPlanAsync(
        IReadOnlyList<EffectIntent> intents,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intents);
        if (intents.Count == 0) return;
        if (intents.Select(item => item.Identity).Distinct().Count() != intents.Count ||
            intents.Select(item => item.IdempotencyKey).Distinct(StringComparer.Ordinal).Count() != intents.Count)
        {
            throw new ArgumentException("An effect plan must have unique intent identities and idempotency keys.", nameof(intents));
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            foreach (EffectIntent intent in intents.OrderBy(item => item.Order))
            {
                await using SqliteCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO canonical_effect_intents (
                        effect_intent_id, transition_run_id, attempt_id, effect_identity, category,
                        effect_order, idempotency_key, status, definition_json, planned_at,
                        workspace_id, run_id, workflow_instance_id, semantic_operation_key,
                        executor_key, executor_version, target_json, payload_json, payload_hash,
                        requiredness, dependencies_json, precondition_json, postcondition_json,
                        reconciliation_policy, row_version, attempt_count
                    ) VALUES (
                        $intent, $transition, $attempt, $semantic, 'Canonical', $order, $idempotency,
                        'Planned', $definition, $planned, $workspace, $run, $workflow, $semantic,
                        $executor, $executor_version, $target, $payload, $payload_hash, $requiredness,
                        $dependencies, $precondition, $postcondition, $reconciliation, 0, 0
                    )
                    ON CONFLICT(idempotency_key) DO NOTHING;
                    """;
                Add(command,
                    ("$intent", intent.Identity.Value),
                    ("$transition", intent.Causality.TransitionRun.Value),
                    ("$attempt", intent.Causality.Attempt.Value),
                    ("$semantic", intent.SemanticOperationKey),
                    ("$order", intent.Order),
                    ("$idempotency", intent.IdempotencyKey),
                    ("$definition", JsonSerializer.Serialize(intent, JsonOptions)),
                    ("$planned", Format(intent.PlannedAt)),
                    ("$workspace", intent.Causality.Workspace.Value),
                    ("$run", intent.Causality.Run.Value),
                    ("$workflow", intent.Causality.WorkflowInstance.Value),
                    ("$executor", intent.Executor.Value),
                    ("$executor_version", intent.ExecutorVersion),
                    ("$target", JsonSerializer.Serialize(intent.Target, JsonOptions)),
                    ("$payload", intent.TypedPayload),
                    ("$payload_hash", intent.TypedPayloadHash),
                    ("$requiredness", intent.Requiredness.ToString()),
                    ("$dependencies", JsonSerializer.Serialize(intent.Dependencies, JsonOptions)),
                    ("$precondition", JsonSerializer.Serialize(intent.Precondition, JsonOptions)),
                    ("$postcondition", JsonSerializer.Serialize(intent.Postcondition, JsonOptions)),
                    ("$reconciliation", intent.ReconciliationPolicy));
                int inserted = await command.ExecuteNonQueryAsync(cancellationToken);
                if (inserted == 1)
                {
                    await AppendEventAsync(connection, transaction, intent.Identity, EffectLifecycle.Planned,
                        "planner", "Required effect intent committed atomically with its plan.", [], intent.PlannedAt,
                        cancellationToken);
                }
                else
                {
                    await using SqliteCommand existingIdentityCommand = connection.CreateCommand();
                    existingIdentityCommand.Transaction = transaction;
                    existingIdentityCommand.CommandText =
                        "SELECT effect_intent_id FROM canonical_effect_intents WHERE idempotency_key = $key;";
                    existingIdentityCommand.Parameters.AddWithValue("$key", intent.IdempotencyKey);
                    string existingIdentity = Convert.ToString(
                        await existingIdentityCommand.ExecuteScalarAsync(cancellationToken),
                        CultureInfo.InvariantCulture)
                        ?? throw new InvalidOperationException("Effect idempotency row disappeared.");
                    EffectWorkItem existing = await ReadRequiredAsync(
                        connection, transaction, new EffectIntentIdentity(existingIdentity), cancellationToken);
                    if (!SemanticallyEquivalent(existing.Intent, intent))
                    {
                        throw new InvalidOperationException("Effect idempotency key resolves to a different semantic intent.");
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<EffectWorkItem>> ScanUnsettledAsync(
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (limit is <= 0 or > 1024) throw new ArgumentOutOfRangeException(nameof(limit));
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT effect_intent_id
            FROM canonical_effect_intents
            WHERE terminal_receipt_id IS NULL
              AND status IN ('Planned', 'Pending', 'Started', 'Unknown', 'Reconciling', 'RetryAuthorized', 'Leased')
              AND (lease_owner IS NULL OR lease_expires_at IS NULL OR lease_expires_at <= $now)
            ORDER BY effect_order, planned_at, effect_intent_id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$limit", limit);
        var identities = new List<EffectIntentIdentity>();
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) identities.Add(new(reader.GetString(0)));
        }

        var items = new List<EffectWorkItem>(identities.Count);
        foreach (EffectIntentIdentity identity in identities)
        {
            items.Add(await ReadRequiredAsync(connection, null, identity, cancellationToken));
        }
        return items;
    }

    public async Task<EffectWorkItem?> ReadAsync(EffectIntentIdentity identity, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        return await ReadCoreAsync(connection, null, identity, cancellationToken);
    }

    public async Task<IReadOnlyList<EffectWorkItem>> ReadPlanAsync(
        TransitionRunIdentity transitionRun,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT effect_intent_id FROM canonical_effect_intents
            WHERE transition_run_id = $transition
            ORDER BY effect_order, planned_at, effect_intent_id;
            """;
        command.Parameters.AddWithValue("$transition", transitionRun.Value);
        var identities = new List<EffectIntentIdentity>();
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) identities.Add(new(reader.GetString(0)));
        }
        var result = new List<EffectWorkItem>(identities.Count);
        foreach (EffectIntentIdentity identity in identities)
        {
            result.Add(await ReadRequiredAsync(connection, null, identity, cancellationToken));
        }
        return result;
    }

    public async Task<IReadOnlyList<EffectWorkItem>> ReadRunAsync(
        RunIdentity run,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT effect_intent_id FROM canonical_effect_intents
            WHERE run_id = $run ORDER BY planned_at,effect_order,effect_intent_id;
            """;
        command.Parameters.AddWithValue("$run", run.Value);
        var identities = new List<EffectIntentIdentity>();
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
            while (await reader.ReadAsync(cancellationToken)) identities.Add(new(reader.GetString(0)));
        var result = new List<EffectWorkItem>(identities.Count);
        foreach (EffectIntentIdentity identity in identities)
        {
            EffectWorkItem? item = await ReadAsync(identity, cancellationToken);
            if (item is not null) result.Add(item);
        }
        return result;
    }

    public async Task<IReadOnlyList<EffectWorkItem>> ReadBySemanticOperationAsync(
        string semanticOperationKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticOperationKey);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT effect_intent_id FROM canonical_effect_intents
            WHERE semantic_operation_key = $semantic
            ORDER BY planned_at, effect_order, effect_intent_id;
            """;
        command.Parameters.AddWithValue("$semantic", semanticOperationKey);
        var identities = new List<EffectIntentIdentity>();
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) identities.Add(new(reader.GetString(0)));
        }
        var result = new List<EffectWorkItem>(identities.Count);
        foreach (EffectIntentIdentity identity in identities)
        {
            result.Add(await ReadRequiredAsync(connection, null, identity, cancellationToken));
        }
        return result;
    }

    public async Task<EffectLease?> TryLeaseAsync(
        EffectIntentIdentity identity,
        long expectedRowVersion,
        string worker,
        DateTimeOffset now,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worker);
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        EffectWorkItem? current = await ReadCoreAsync(connection, transaction, identity, cancellationToken);
        if (current is null || current.RowVersion != expectedRowVersion || current.Receipt is not null ||
            (current.LeaseOwner is not null && current.LeaseExpiresAt > now))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return null;
        }

        long leaseExpectedVersion = expectedRowVersion;
        if (current.State == EffectLifecycle.Leased)
        {
            await using SqliteCommand release = connection.CreateCommand();
            release.Transaction = transaction;
            release.CommandText = """
                UPDATE canonical_effect_intents
                SET status = 'Planned', lease_owner = NULL, lease_expires_at = NULL,
                    row_version = row_version + 1
                WHERE effect_intent_id = $intent AND row_version = $version
                  AND lease_expires_at <= $now;
                """;
            Add(release, ("$intent", identity.Value), ("$version", expectedRowVersion), ("$now", Format(now)));
            if (await release.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return null;
            }
            await AppendEventAsync(connection, transaction, identity, EffectLifecycle.Planned, worker,
                "Expired unstarted lease released for deterministic rediscovery.", [], now, cancellationToken);
            current = current with { State = EffectLifecycle.Planned, RowVersion = expectedRowVersion + 1 };
            leaseExpectedVersion++;
        }

        EffectLifecyclePolicy.RequireTransition(current.State, EffectLifecycle.Leased);
        DateTimeOffset expires = now + duration;
        await using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE canonical_effect_intents
            SET status = 'Leased', lease_owner = $worker, lease_expires_at = $expires,
                row_version = row_version + 1, attempt_count = attempt_count + 1
            WHERE effect_intent_id = $intent AND row_version = $version
              AND terminal_receipt_id IS NULL
              AND (lease_owner IS NULL OR lease_expires_at IS NULL OR lease_expires_at <= $now);
            """;
        Add(update, ("$worker", worker), ("$expires", Format(expires)), ("$intent", identity.Value),
            ("$version", leaseExpectedVersion), ("$now", Format(now)));
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return null;
        }
        await AppendEventAsync(connection, transaction, identity, EffectLifecycle.Leased, worker,
            "Effect lease acquired by compare-and-set.", [], now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new EffectLease(current.Intent, leaseExpectedVersion + 1, worker, expires, current.State);
    }

    public async Task<EffectWorkItem> AppendLifecycleAsync(
        EffectIntentIdentity identity,
        long expectedRowVersion,
        EffectLifecycle state,
        string worker,
        string explanation,
        IReadOnlyList<string> evidence,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worker);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        EffectWorkItem current = await ReadRequiredAsync(connection, transaction, identity, cancellationToken);
        if (current.RowVersion != expectedRowVersion) throw new InvalidOperationException("Effect row-version conflict.");
        EffectLifecyclePolicy.RequireTransition(current.State, state);
        bool retainLease = state is EffectLifecycle.Started or EffectLifecycle.Reconciling;
        await using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE canonical_effect_intents
            SET status = $state, row_version = row_version + 1,
                lease_owner = CASE WHEN $retain = 1 THEN lease_owner ELSE NULL END,
                lease_expires_at = CASE WHEN $retain = 1 THEN lease_expires_at ELSE NULL END,
                failure = CASE WHEN $state IN ('Failed','Stalled','Unknown','HumanActionRequired') THEN $explanation ELSE failure END
            WHERE effect_intent_id = $intent AND row_version = $version;
            """;
        Add(update, ("$state", state.ToString()), ("$retain", retainLease ? 1 : 0),
            ("$explanation", explanation), ("$intent", identity.Value), ("$version", expectedRowVersion));
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Effect row-version conflict.");
        await AppendEventAsync(connection, transaction, identity, state, worker, explanation, evidence, recordedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await ReadAsync(identity, cancellationToken))!;
    }

    public async Task<EffectWorkItem> RecordReceiptAsync(
        EffectIntentIdentity identity,
        long expectedRowVersion,
        EffectReceipt receipt,
        string worker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.Intent != identity || receipt.Identity.IsEmpty || !receipt.PostconditionSatisfied)
            throw new ArgumentException("A terminal success receipt must identify the intent and verify its postcondition.", nameof(receipt));
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        EffectWorkItem current = await ReadRequiredAsync(connection, transaction, identity, cancellationToken);
        if (current.RowVersion != expectedRowVersion) throw new InvalidOperationException("Effect row-version conflict.");
        EffectLifecyclePolicy.RequireTransition(current.State, EffectLifecycle.Succeeded);

        await using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO canonical_effect_receipts (
                    receipt_id, effect_intent_id, executor_key, executor_version,
                    observed_target_identity, before_facts_json, after_facts_json,
                    postcondition_satisfied, external_correlation, evidence_json, recorded_at
                ) VALUES ($receipt, $intent, $executor, $version, $target, $before, $after, 1,
                          $correlation, $evidence, $recorded);
                """;
            Add(insert, ("$receipt", receipt.Identity.Value), ("$intent", identity.Value),
                ("$executor", receipt.Executor.Value), ("$version", receipt.ExecutorVersion),
                ("$target", receipt.ObservedTargetIdentity), ("$before", receipt.BeforeFacts),
                ("$after", receipt.AfterFacts), ("$correlation", receipt.ExternalCorrelation),
                ("$evidence", JsonSerializer.Serialize(receipt.Evidence, JsonOptions)), ("$recorded", Format(receipt.RecordedAt)));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (SqliteCommand update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE canonical_effect_intents
                SET status = 'Succeeded', terminal_receipt_id = $receipt, row_version = row_version + 1,
                    lease_owner = NULL, lease_expires_at = NULL, completed_at = $recorded
                WHERE effect_intent_id = $intent AND row_version = $version AND terminal_receipt_id IS NULL;
                """;
            Add(update, ("$receipt", receipt.Identity.Value), ("$recorded", Format(receipt.RecordedAt)),
                ("$intent", identity.Value), ("$version", expectedRowVersion));
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Effect row-version conflict.");
        }
        await AppendEventAsync(connection, transaction, identity, EffectLifecycle.Succeeded, worker,
            "Verified effect receipt recorded.", receipt.Evidence, receipt.RecordedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await ReadAsync(identity, cancellationToken))!;
    }

    public async Task RecordReconciliationAsync(
        EffectIntentIdentity identity,
        long expectedRowVersion,
        EffectReconciliationObservation observation,
        string worker,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observation);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        EffectWorkItem current = await ReadRequiredAsync(connection, transaction, identity, cancellationToken);
        if (current.RowVersion != expectedRowVersion || current.State != EffectLifecycle.Reconciling ||
            !string.Equals(current.LeaseOwner, worker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Reconciliation observation lost its effect lease or row version.");
        }
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO canonical_effect_reconciliation_attempts (
                reconciliation_id, effect_intent_id, worker_id, verdict,
                before_facts_json, after_facts_json, external_correlation,
                evidence_json, recorded_at
            ) VALUES ($reconciliation, $intent, $worker, $verdict, $before, $after,
                      $correlation, $evidence, $recorded);
            """;
        Add(command,
            ("$reconciliation", EffectReconciliationIdentity.New().Value),
            ("$intent", identity.Value),
            ("$worker", worker),
            ("$verdict", observation.Verdict.ToString()),
            ("$before", observation.BeforeFacts),
            ("$after", observation.AfterFacts),
            ("$correlation", observation.ExternalCorrelation),
            ("$evidence", JsonSerializer.Serialize(observation.Evidence, JsonOptions)),
            ("$recorded", Format(recordedAt)));
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        return connection;
    }

    private static async Task<EffectWorkItem> ReadRequiredAsync(SqliteConnection connection, SqliteTransaction? transaction,
        EffectIntentIdentity identity, CancellationToken cancellationToken) =>
        await ReadCoreAsync(connection, transaction, identity, cancellationToken)
        ?? throw new InvalidOperationException($"Effect intent '{identity}' was not found.");

    private static async Task<EffectWorkItem?> ReadCoreAsync(SqliteConnection connection, SqliteTransaction? transaction,
        EffectIntentIdentity identity, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT definition_json, status, row_version, lease_owner, lease_expires_at, attempt_count,
                   terminal_receipt_id
            FROM canonical_effect_intents WHERE effect_intent_id = $intent;
            """;
        command.Parameters.AddWithValue("$intent", identity.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        EffectIntent intent = JsonSerializer.Deserialize<EffectIntent>(reader.GetString(0), JsonOptions)
            ?? throw new InvalidOperationException("Effect intent document is invalid.");
        EffectLifecycle state = Enum.Parse<EffectLifecycle>(reader.GetString(1));
        long rowVersion = reader.GetInt64(2);
        string? leaseOwner = reader.IsDBNull(3) ? null : reader.GetString(3);
        DateTimeOffset? leaseExpiry = reader.IsDBNull(4) ? null : DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture);
        int attemptCount = reader.GetInt32(5);
        string? receiptId = reader.IsDBNull(6) ? null : reader.GetString(6);
        await reader.DisposeAsync();
        EffectReceipt? receipt = receiptId is null ? null : await ReadReceiptAsync(connection, transaction, receiptId, cancellationToken);
        IReadOnlyList<EffectLifecycleEvent> events = await ReadEventsAsync(connection, transaction, identity, cancellationToken);
        return new EffectWorkItem(intent, state, rowVersion, leaseOwner, leaseExpiry, attemptCount, receipt, events);
    }

    private static async Task<EffectReceipt?> ReadReceiptAsync(SqliteConnection connection, SqliteTransaction? transaction,
        string receiptId, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT receipt_id, effect_intent_id, executor_key, executor_version, observed_target_identity,
                   before_facts_json, after_facts_json, postcondition_satisfied, external_correlation,
                   evidence_json, recorded_at
            FROM canonical_effect_receipts WHERE receipt_id = $receipt;
            """;
        command.Parameters.AddWithValue("$receipt", receiptId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new EffectReceipt(new(reader.GetString(0)), new(reader.GetString(1)), new(reader.GetString(2)), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetBoolean(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(9), JsonOptions) ?? [],
            DateTimeOffset.Parse(reader.GetString(10), CultureInfo.InvariantCulture));
    }

    private static async Task<IReadOnlyList<EffectLifecycleEvent>> ReadEventsAsync(SqliteConnection connection,
        SqliteTransaction? transaction, EffectIntentIdentity identity, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT event_id, lifecycle, worker_id, explanation, evidence_json, recorded_at
            FROM canonical_effect_lifecycle_events WHERE effect_intent_id = $intent ORDER BY event_id;
            """;
        command.Parameters.AddWithValue("$intent", identity.Value);
        var result = new List<EffectLifecycleEvent>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new EffectLifecycleEvent(reader.GetInt64(0), identity, Enum.Parse<EffectLifecycle>(reader.GetString(1)),
                reader.GetString(2), reader.GetString(3),
                JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(4), JsonOptions) ?? [],
                DateTimeOffset.Parse(reader.GetString(5), CultureInfo.InvariantCulture)));
        }
        return result;
    }

    private static async Task AppendEventAsync(SqliteConnection connection, SqliteTransaction transaction,
        EffectIntentIdentity identity, EffectLifecycle lifecycle, string worker, string explanation,
        IReadOnlyList<string> evidence, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO canonical_effect_lifecycle_events (
                effect_intent_id, lifecycle, worker_id, explanation, evidence_json, recorded_at
            ) VALUES ($intent, $lifecycle, $worker, $explanation, $evidence, $recorded);
            """;
        Add(command, ("$intent", identity.Value), ("$lifecycle", lifecycle.ToString()), ("$worker", worker),
            ("$explanation", explanation), ("$evidence", JsonSerializer.Serialize(evidence, JsonOptions)),
            ("$recorded", Format(recordedAt)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Add(SqliteCommand command, params (string Name, object? Value)[] values)
    {
        foreach ((string name, object? value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static bool SemanticallyEquivalent(EffectIntent left, EffectIntent right) =>
        left.Causality == right.Causality &&
        left.SemanticOperationKey == right.SemanticOperationKey &&
        left.Executor == right.Executor &&
        left.ExecutorVersion == right.ExecutorVersion &&
        left.Target == right.Target &&
        left.TypedPayloadHash == right.TypedPayloadHash &&
        left.Order == right.Order &&
        left.Dependencies.SequenceEqual(right.Dependencies) &&
        left.Requiredness == right.Requiredness &&
        left.Precondition == right.Precondition &&
        left.Postcondition == right.Postcondition &&
        left.ReconciliationPolicy == right.ReconciliationPolicy &&
        left.IdempotencyKey == right.IdempotencyKey;
}

public sealed class CanonicalEffectPlanSettlementStore(
    Repository _repository,
    IReadOnlyList<WorkflowDefinition>? _definitions = null) : IEffectPlanSettlementStore
{
    private static readonly JsonSerializerOptions SettlementJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task RecordOutcomeAsync(
        TransitionRunIdentity transitionRun,
        RuntimeOutcomeKind outcome,
        string explanation,
        CancellationToken cancellationToken)
    {
        TransitionDurableState state = outcome switch
        {
            RuntimeOutcomeKind.Stalled => TransitionDurableState.Stalled,
            RuntimeOutcomeKind.Failed => TransitionDurableState.Failed,
            RuntimeOutcomeKind.RecoveryRequired => TransitionDurableState.EffectsPartiallyApplied,
            _ => TransitionDurableState.EffectsPending,
        };
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE canonical_transition_runs
            SET state = $state, outcome = $outcome, explanation = $explanation,
                completed_at = CASE WHEN $outcome IN ('Stalled','Failed') THEN $now ELSE completed_at END
            WHERE run_id = $transition;
            UPDATE attempts
            SET outcome = $outcome,
                completed_at = CASE WHEN $outcome IN ('Stalled','Failed') THEN COALESCE(completed_at, $now) ELSE completed_at END
            WHERE transition_run_id = $transition;
            UPDATE canonical_workflow_states
            SET outcome = $outcome, updated_at = $now
            WHERE workflow_identity = (
                SELECT workflow_identity
                FROM canonical_transition_runs
                WHERE run_id = $transition
            );
            """;
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$outcome", outcome.ToString());
        command.Parameters.AddWithValue("$explanation", explanation);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$transition", transitionRun.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> TrySettleAsync(
        TransitionRunIdentity transitionRun,
        CancellationToken cancellationToken)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await using SqliteCommand readiness = connection.CreateCommand();
        readiness.Transaction = transaction;
        readiness.CommandText = """
            SELECT COUNT(*),
                   SUM(CASE WHEN intent.status = 'Succeeded'
                                  AND receipt.receipt_id IS NOT NULL
                                  AND receipt.postcondition_satisfied = 1
                            THEN 0 ELSE 1 END)
            FROM canonical_effect_intents AS intent
            LEFT JOIN canonical_effect_receipts AS receipt
              ON receipt.receipt_id = intent.terminal_receipt_id
            WHERE intent.transition_run_id = $transition;
            """;
        readiness.Parameters.AddWithValue("$transition", transitionRun.Value);
        await using SqliteDataReader reader = await readiness.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        long count = reader.GetInt64(0);
        long unsettled = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
        await reader.DisposeAsync();
        if (count == 0 || unsettled != 0)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return false;
        }

        string now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        TransitionProgression progression = await ReadProgressionAsync(
            connection, transaction, transitionRun, cancellationToken);
        await using SqliteCommand settle = connection.CreateCommand();
        settle.Transaction = transaction;
        settle.CommandText = """
            UPDATE canonical_transition_runs
            SET state = 'Completed', outcome = 'Completed', completed_at = $now,
                explanation = 'All required effect postconditions have verified receipts.'
            WHERE run_id = $transition;
            UPDATE attempts
            SET outcome = 'Completed', completed_at = COALESCE(completed_at, $now)
            WHERE transition_run_id = $transition;
            UPDATE canonical_stage_states
            SET state = $completed_stage_state, updated_at = $now
            WHERE workflow_identity = $workflow AND stage_identity = $stage;
            INSERT INTO canonical_stage_states (
                workflow_identity, stage_identity, state, updated_at, evidence_json
            )
            SELECT $workflow, $current_stage, 'Active', $now, '[]'
            WHERE $current_stage IS NOT NULL AND $current_stage <> $stage
            ON CONFLICT(workflow_identity, stage_identity) DO UPDATE SET
                state = excluded.state, updated_at = excluded.updated_at;
            UPDATE canonical_workflow_states
            SET state = $workflow_state, current_stage = $current_stage,
                outcome = $workflow_outcome, updated_at = $now
            WHERE workflow_identity = $workflow;
            INSERT INTO canonical_transition_evidence (
                run_id, transition_identity, event_name, recorded_at, state,
                explanation, evidence_json, document_json
            )
            SELECT run_id, transition_identity, 'EffectPlanSettled', $now, 'Completed',
                   'All required effect postconditions have verified receipts.', '[]', '{}'
            FROM canonical_transition_runs WHERE run_id = $transition
              AND NOT EXISTS (
                  SELECT 1 FROM canonical_transition_evidence
                  WHERE run_id = $transition AND event_name = 'EffectPlanSettled'
              );
            """;
        settle.Parameters.AddWithValue("$now", now);
        settle.Parameters.AddWithValue("$transition", transitionRun.Value);
        settle.Parameters.AddWithValue("$workflow", progression.Workflow.Value);
        settle.Parameters.AddWithValue("$stage", progression.Stage.Value);
        settle.Parameters.AddWithValue("$completed_stage_state", progression.CompletedStageState.ToString());
        settle.Parameters.AddWithValue("$current_stage", progression.CurrentStage?.Value ?? (object)DBNull.Value);
        settle.Parameters.AddWithValue("$workflow_state", progression.WorkflowState.ToString());
        settle.Parameters.AddWithValue("$workflow_outcome", progression.WorkflowOutcome.ToString());
        await settle.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task<TransitionProgression> ReadProgressionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TransitionRunIdentity transitionRun,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT workflow_identity, stage_identity, transition_identity
            FROM canonical_transition_runs WHERE run_id = $transition;
            """;
        command.Parameters.AddWithValue("$transition", transitionRun.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException($"Transition run '{transitionRun}' disappeared before settlement.");
        }
        var workflowIdentity = new WorkflowIdentity(reader.GetString(0));
        var stageIdentity = new WorkflowStageIdentity(reader.GetString(1));
        var transitionIdentity = new WorkflowTransitionIdentity(reader.GetString(2));
        await reader.DisposeAsync();

        IReadOnlyList<WorkflowDefinition> definitions = _definitions ?? CanonicalWorkflowCatalog.Current.Workflows;
        WorkflowDefinition? workflow = definitions.SingleOrDefault(item => item.Identity == workflowIdentity);
        WorkflowStageDefinition? stage = workflow?.Stages.SingleOrDefault(item => item.Identity == stageIdentity);
        WorkflowTransitionDefinition? definition = workflow?.Transitions.SingleOrDefault(item => item.Identity == transitionIdentity);
        if (workflow is null || stage is null || definition is null)
        {
            return new TransitionProgression(
                workflowIdentity, stageIdentity, WorkflowResolutionState.Active,
                WorkflowResolutionState.Resumable, stageIdentity, RuntimeOutcomeKind.Waiting);
        }
        bool completesStage = TransitionCompletesStage(workflow, stage, definition);
        if (!completesStage)
        {
            return new TransitionProgression(
                workflowIdentity, stageIdentity, WorkflowResolutionState.Active,
                WorkflowResolutionState.Resumable, stageIdentity, RuntimeOutcomeKind.Waiting);
        }
        if (stage.AllowedSuccessors.Count == 0)
        {
            return new TransitionProgression(
                workflowIdentity, stageIdentity, WorkflowResolutionState.Completed,
                WorkflowResolutionState.Completed, null, RuntimeOutcomeKind.Completed);
        }

        WorkflowStageIdentity next = await ResolveNextStageAsync(
            connection, transaction, transitionRun, workflow, stage, definition, cancellationToken);
        return new TransitionProgression(
            workflowIdentity, stageIdentity, WorkflowResolutionState.Completed,
            WorkflowResolutionState.Resumable, next, RuntimeOutcomeKind.Waiting);
    }

    private static bool TransitionCompletesStage(
        WorkflowDefinition workflow,
        WorkflowStageDefinition stage,
        WorkflowTransitionDefinition definition)
    {
        if (workflow.Identity == WorkflowIdentity.Plan) return stage.Transitions[^1] == definition.Identity;
        if (workflow.Identity == WorkflowIdentity.Execute &&
            stage.Identity.Value is "Execution Continuity" or "Completion")
        {
            return stage.Transitions[^1] == definition.Identity;
        }
        return workflow.Identity != WorkflowIdentity.TraditionalRoadmap ||
            stage.Identity.Value != "Epic Preparation" ||
            definition.Identity.Value != "AuditExistingEpic";
    }

    private static async Task<WorkflowStageIdentity> ResolveNextStageAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        TransitionRunIdentity transitionRun,
        WorkflowDefinition workflow,
        WorkflowStageDefinition stage,
        WorkflowTransitionDefinition definition,
        CancellationToken cancellationToken)
    {
        if (workflow.Identity != WorkflowIdentity.Execute || stage.Identity.Value != "Completion" ||
            definition.Identity.Value != "InterpretCompletionRoute")
        {
            return stage.AllowedSuccessors[0];
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT document_json FROM canonical_transition_evidence
            WHERE run_id = $transition AND event_name = 'RawPromptOutputCaptured'
            ORDER BY evidence_id DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$transition", transitionRun.Value);
        string? json = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        PromptExecutionResult? output = string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<PromptExecutionResult>(json, SettlementJsonOptions);
        string? raw = output?.RawOutput
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("| Should Close Epic |", StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Trim('|').Split('|')[1].Trim())
            .SingleOrDefault();
        if (!bool.TryParse(raw, out bool shouldClose))
        {
            throw new InvalidOperationException(
                "Canonical completion-route output does not contain an unambiguous `Should Close Epic` boolean.");
        }
        string successor = shouldClose ? "Workflow Completion" : "Execution Readiness";
        WorkflowStageIdentity selected = stage.AllowedSuccessors.SingleOrDefault(item => item.Value == successor);
        return selected.IsEmpty
            ? throw new InvalidOperationException($"Completion route selected undeclared successor '{successor}'.")
            : selected;
    }

    private sealed record TransitionProgression(
        WorkflowIdentity Workflow,
        WorkflowStageIdentity Stage,
        WorkflowResolutionState CompletedStageState,
        WorkflowResolutionState WorkflowState,
        WorkflowStageIdentity? CurrentStage,
        RuntimeOutcomeKind WorkflowOutcome);
}
