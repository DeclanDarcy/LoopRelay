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

    /// <summary>
    /// Test-only observability: every connection this store opens is offered here once it is open
    /// and schema-verified, immediately before the calling read or write issues its own statements.
    /// A test installs a SQLite authorizer on it and counts the statements a path really prepares.
    /// <para>
    /// This exists because there is no other way to observe that cost. Microsoft.Data.Sqlite
    /// exposes no statement hook, SQLitePCLRaw's only statement-level hook - the authorizer - is
    /// per connection, and this store opens its connections itself with pooling disabled, so a test
    /// can never reach the handle a read actually ran on. Without this seam, a statement-count
    /// assertion can only restate a hand-maintained model, which is exactly what a per-row
    /// regression would keep satisfying. Instance-scoped, so a test observes only its own store.
    /// </para>
    /// </summary>
    internal Action<SqliteConnection>? ConnectionObserverForTesting { get; set; }

    /// <summary>
    /// Fires once for every statement this store issues through <see cref="CreateCommand"/>, which
    /// covers the unsettled scan and the three per-item read helpers behind it. Test seam only: the
    /// scan's cost is statements against a growing event history, not connections, so the
    /// connection observer cannot see it.
    /// </summary>
    internal Action<SqliteCommand>? CommandObserverForTesting { get; set; }

    private SqliteCommand CreateCommand(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        CommandObserverForTesting?.Invoke(command);
        return command;
    }

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
                        reconciliation_policy
                    ) VALUES (
                        $intent, $transition, $attempt, $semantic, 'Canonical', $order, $idempotency,
                        'Planned', $definition, $planned, $workspace, $run, $workflow, $semantic,
                        $executor, $executor_version, $target, $payload, $payload_hash, $requiredness,
                        $dependencies, $precondition, $postcondition, $reconciliation
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

    public async Task<IReadOnlyList<EffectScanRow>> ScanUnsettledAsync(
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        IReadOnlySet<EffectIntentIdentity>? only = null)
    {
        if (limit is <= 0 or > 1024) throw new ArgumentOutOfRangeException(nameof(limit));
        if (only is { Count: 0 }) return [];
        // The identity restriction belongs in the predicate, not in a filter over the result: LIMIT
        // bounds the scan window, so a requested intent ordered past the window would otherwise be
        // scanned away and the targeted run would silently settle nothing. For the same reason the
        // window is widened to at least the size of the requested set. `effect_intent_id IN (...)`
        // resolves on the primary key rather than idx_effect_intents_unsettled, which may turn the
        // ORDER BY into a sort; that is immaterial at the row counts this scan bounds.
        string restriction = only is null
            ? string.Empty
            : $" AND effect_intent_id IN ({string.Join(", ", Enumerable.Range(0, only.Count).Select(index => $"$only{index}"))})";
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = CreateCommand(connection);
        // 'Started' and 'Leased' are retired tokens kept only so pre-cut rows are still found;
        // ParseStatus maps both to Planned. There is no lease-expiry disjunction any more, because
        // the lease columns are gone: a row is discoverable on its status alone.
        command.CommandText = $"""
            SELECT definition_json, status
            FROM canonical_effect_intents
            WHERE status IN ('Planned', 'Pending', 'Started', 'Unknown', 'Reconciling', 'RetryAuthorized', 'Leased'){restriction}
            ORDER BY effect_order, planned_at, effect_intent_id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", only is null ? limit : Math.Max(limit, only.Count));
        if (only is not null)
        {
            int index = 0;
            foreach (EffectIntentIdentity identity in only)
            {
                command.Parameters.AddWithValue($"$only{index++}", identity.Value);
            }
        }

        var rows = new List<EffectScanRow>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            EffectIntent intent = JsonSerializer.Deserialize<EffectIntent>(reader.GetString(0), JsonOptions)
                ?? throw new InvalidOperationException("Effect intent document is invalid.");
            rows.Add(new EffectScanRow(
                intent,
                ParseStatus(reader.GetString(1))));
        }
        return rows;
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
        return await ReadScopeAsync(
            connection,
            "intent.transition_run_id = $scope",
            "intent.effect_order, intent.planned_at, intent.effect_intent_id",
            transitionRun.Value,
            cancellationToken);
    }

    public async Task<IReadOnlyList<EffectWorkItem>> ReadRunAsync(
        RunIdentity run,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        return await ReadScopeAsync(
            connection,
            "intent.run_id = $scope",
            "intent.planned_at, intent.effect_order, intent.effect_intent_id",
            run.Value,
            cancellationToken);
    }

    public async Task<IReadOnlyList<EffectWorkItem>> ReadBySemanticOperationAsync(
        string semanticOperationKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticOperationKey);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        return await ReadScopeAsync(
            connection,
            "intent.semantic_operation_key = $scope",
            "intent.planned_at, intent.effect_order, intent.effect_intent_id",
            semanticOperationKey,
            cancellationToken);
    }

    /// <summary>
    /// Answers one dependency's gate from columns alone. <c>definition_json</c> is never selected:
    /// it is the serialised intent, and for filesystem-write effects that document embeds the file
    /// content being written, so a yes/no question must not pay for it. Every value the gate needs
    /// — lifecycle status, effect order, planned time, dependency list, owning transition run — is
    /// a first-class column written identically by both writers into
    /// <c>canonical_effect_intents</c> (this store's <c>AppendPlanAsync</c> and
    /// <c>CanonicalWorkflowPersistenceStore.CommitTransitionAsync</c>).
    /// <para>
    /// Every call reads live rows. Nothing here may be memoised: the barrier exists precisely
    /// because durable state changes underneath a worker pass.
    /// </para>
    /// </summary>
    public async Task<bool> DependencySatisfiedAsync(
        EffectIntent candidate,
        EffectIntentIdentity dependency,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        string transitionRun;
        await using (SqliteCommand settled = connection.CreateCommand())
        {
            settled.CommandText = """
                SELECT dependency.transition_run_id,
                       CASE WHEN dependency.status = 'Succeeded' THEN 1 ELSE 0 END
                FROM canonical_effect_intents AS dependency
                WHERE dependency.effect_intent_id = $dependency;
                """;
            settled.Parameters.AddWithValue("$dependency", dependency.Value);
            await using SqliteDataReader reader = await settled.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return false;
            if (reader.GetInt64(1) != 1) return false;
            transitionRun = reader.GetString(0);
        }

        // The durable barrier, as `EffectWorker.DependenciesSettledAsync` describes it: a sibling
        // ordered at or before the candidate, planned after it, sharing this dependency and not yet
        // settled, holds the candidate back even though the dependency itself has succeeded.
        // This must stay a question asked of the database. Answering it from anything already in
        // memory — a plan read earlier in the pass, a previous dependency's answer — is what lets a
        // pre-planned publication effect overtake the child mutation it is meant to publish.
        await using SqliteCommand barrier = connection.CreateCommand();
        barrier.CommandText = """
            SELECT sibling.planned_at
            FROM canonical_effect_intents AS sibling
            WHERE sibling.transition_run_id = $transition
              AND sibling.effect_intent_id <> $candidate
              AND sibling.effect_order <= $order
              AND EXISTS (
                  SELECT 1 FROM json_each(sibling.dependencies_json) AS element
                  WHERE json_extract(element.value, '$.value') = $dependency)
              AND sibling.status <> 'Succeeded';
            """;
        Add(barrier, ("$transition", transitionRun), ("$candidate", candidate.Identity.Value),
            ("$order", candidate.Order), ("$dependency", dependency.Value));
        await using SqliteDataReader siblings = await barrier.ExecuteReaderAsync(cancellationToken);
        while (await siblings.ReadAsync(cancellationToken))
        {
            // The "planned after this candidate" half of the barrier is compared here rather than
            // in SQL: `planned_at` is an ISO-8601 string whose offset is whatever the writer held,
            // so ordering it as text is only correct while every writer normalises to UTC. Parsing
            // restores the instant comparison the barrier has always made.
            if (DateTimeOffset.Parse(siblings.GetString(0), CultureInfo.InvariantCulture) > candidate.PlannedAt)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Parses a persisted status. <c>Leased</c> and <c>Started</c> were retired with the lease and
    /// the start marker, but a workspace written before that cut can still hold either token on an
    /// unsettled row, and its lifecycle event history holds them permanently. Both meant
    /// "discovered, not settled", which is <c>Planned</c>; mapping them here keeps those rows
    /// readable and re-executable without a schema migration, and the two literals stay in the scan
    /// predicate so the rows are still discovered.
    /// </summary>
    private static EffectLifecycle ParseStatus(string status) => status switch
    {
        "Leased" or "Started" => EffectLifecycle.Planned,
        _ => Enum.Parse<EffectLifecycle>(status),
    };

    public async Task<EffectWorkItem> AppendLifecycleAsync(
        EffectIntentIdentity identity,
        EffectLifecycle state,
        string worker,
        string explanation,
        IReadOnlyList<string> evidence,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worker);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);
        // This UPDATE writes `status` and never writes `terminal_receipt_id`, and every durable gate
        // now reads settlement off `status` alone. Refused before any I/O so the only way for a row to
        // reach 'Succeeded' is `RecordReceiptAsync`, which writes the status and the receipt pointer
        // in the same statement.
        EffectLifecyclePolicy.RequireAppendableState(state);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        // The state this decides on is re-read inside the write transaction, never carried in from
        // the caller: `BEGIN IMMEDIATE` takes the write lock before this read, so `current.State` is
        // the committed state no other writer can move until this transaction ends. That is what
        // makes the transition check a real guard rather than a check against a stale snapshot, and
        // it is why no caller-supplied row version is needed to establish the same thing.
        EffectWorkItem current = await ReadRequiredAsync(connection, transaction, identity, cancellationToken);
        EffectLifecyclePolicy.RequireTransition(current.State, state);
        await using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE canonical_effect_intents
            SET status = $state,
                failure = CASE WHEN $state IN ('Failed','Stalled','Unknown','HumanActionRequired') THEN $explanation ELSE failure END
            WHERE effect_intent_id = $intent;
            """;
        Add(update, ("$state", state.ToString()),
            ("$explanation", explanation), ("$intent", identity.Value));
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
        {
            throw new InvalidOperationException("Effect intent row disappeared during lifecycle append.");
        }
        long sequence = await AppendEventAsync(
            connection, transaction, identity, state, worker, explanation, evidence, recordedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // Projected from what this transaction wrote rather than re-read after commit.
        return current with
        {
            State = state,
            Events = [.. current.Events, new EffectLifecycleEvent(
                sequence, identity, state, worker, explanation, evidence, recordedAt)],
        };
    }

    public async Task<EffectWorkItem> RecordReceiptAsync(
        EffectIntentIdentity identity,
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
                SET status = 'Succeeded', terminal_receipt_id = $receipt,
                    completed_at = $recorded
                WHERE effect_intent_id = $intent AND status <> 'Succeeded';
                """;
            Add(update, ("$receipt", receipt.Identity.Value), ("$recorded", Format(receipt.RecordedAt)),
                ("$intent", identity.Value));
            // `status <> 'Succeeded'` is the guard that matters and it stays: it is what makes a
            // second terminal write for an already-settled row a refusal rather than an overwrite,
            // and unlike a row version it is evaluated against durable state rather than against a
            // number the caller carried in.
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                throw new InvalidOperationException(
                    "Effect intent row was already terminal or disappeared during receipt recording.");
            }
        }
        const string explanation = "Verified effect receipt recorded.";
        long sequence = await AppendEventAsync(connection, transaction, identity, EffectLifecycle.Succeeded, worker,
            explanation, receipt.Evidence, receipt.RecordedAt, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // Projected from what this transaction wrote rather than re-read after commit; the UPDATE
        // above is what makes this receipt terminal.
        return current with
        {
            State = EffectLifecycle.Succeeded,
            Receipt = receipt,
            Events = [.. current.Events, new EffectLifecycleEvent(
                sequence, identity, EffectLifecycle.Succeeded, worker, explanation, receipt.Evidence, receipt.RecordedAt)],
        };
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        ConnectionObserverForTesting?.Invoke(connection);
        return connection;
    }

    private async Task<EffectWorkItem> ReadRequiredAsync(SqliteConnection connection, SqliteTransaction? transaction,
        EffectIntentIdentity identity, CancellationToken cancellationToken) =>
        await ReadCoreAsync(connection, transaction, identity, cancellationToken)
        ?? throw new InvalidOperationException($"Effect intent '{identity}' was not found.");

    private async Task<EffectWorkItem?> ReadCoreAsync(SqliteConnection connection, SqliteTransaction? transaction,
        EffectIntentIdentity identity, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = CreateCommand(connection, transaction);
        command.CommandText = """
            SELECT definition_json, status, terminal_receipt_id
            FROM canonical_effect_intents WHERE effect_intent_id = $intent;
            """;
        command.Parameters.AddWithValue("$intent", identity.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        EffectIntent intent = JsonSerializer.Deserialize<EffectIntent>(reader.GetString(0), JsonOptions)
            ?? throw new InvalidOperationException("Effect intent document is invalid.");
        EffectLifecycle state = ParseStatus(reader.GetString(1));
        string? receiptId = reader.IsDBNull(2) ? null : reader.GetString(2);
        await reader.DisposeAsync();
        EffectReceipt? receipt = receiptId is null ? null : await ReadReceiptAsync(connection, transaction, receiptId, cancellationToken);
        IReadOnlyList<EffectLifecycleEvent> events = await ReadEventsAsync(connection, transaction, identity, cancellationToken);
        return new EffectWorkItem(intent, state, receipt, events);
    }

    /// <summary>
    /// Hydrates every effect work item in a scope with three set-based statements — intents, then
    /// their receipts, then their lifecycle events — instead of one identity query plus a core row,
    /// an event history and a receipt per item.
    /// <para>
    /// Three statements rather than one wide join: joining intents to lifecycle events in a single
    /// result set would repeat <c>definition_json</c>, which embeds file contents, once per event.
    /// </para>
    /// <para>
    /// <paramref name="scopePredicate"/> and <paramref name="ordering"/> are compile-time literals
    /// supplied by this class; <paramref name="scopeValue"/> is the only caller data and it is
    /// always bound as the <c>$scope</c> parameter.
    /// </para>
    /// </summary>
    private static async Task<IReadOnlyList<EffectWorkItem>> ReadScopeAsync(
        SqliteConnection connection,
        string scopePredicate,
        string ordering,
        string scopeValue,
        CancellationToken cancellationToken)
    {
        var rows = new List<(EffectIntentIdentity Identity, EffectIntent Intent, EffectLifecycle State)>();
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT intent.effect_intent_id, intent.definition_json, intent.status
                FROM canonical_effect_intents AS intent
                WHERE {scopePredicate}
                ORDER BY {ordering};
                """;
            command.Parameters.AddWithValue("$scope", scopeValue);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add((
                    new(reader.GetString(0)),
                    JsonSerializer.Deserialize<EffectIntent>(reader.GetString(1), JsonOptions)
                        ?? throw new InvalidOperationException("Effect intent document is invalid."),
                    ParseStatus(reader.GetString(2))));
            }
        }
        // Nothing to stitch receipts or events onto, so neither statement is worth issuing.
        if (rows.Count == 0) return [];

        var receipts = new Dictionary<EffectIntentIdentity, EffectReceipt>();
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT intent.effect_intent_id, receipt.receipt_id, receipt.effect_intent_id,
                       receipt.executor_key, receipt.executor_version, receipt.observed_target_identity,
                       receipt.before_facts_json, receipt.after_facts_json, receipt.postcondition_satisfied,
                       receipt.external_correlation, receipt.evidence_json, receipt.recorded_at
                FROM canonical_effect_intents AS intent
                JOIN canonical_effect_receipts AS receipt
                  ON receipt.receipt_id = intent.terminal_receipt_id
                WHERE {scopePredicate};
                """;
            command.Parameters.AddWithValue("$scope", scopeValue);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                receipts[new(reader.GetString(0))] = MapReceipt(reader, offset: 1);
            }
        }

        var events = new Dictionary<EffectIntentIdentity, List<EffectLifecycleEvent>>();
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT event.effect_intent_id, event.event_id, event.lifecycle, event.worker_id,
                       event.explanation, event.evidence_json, event.recorded_at
                FROM canonical_effect_lifecycle_events AS event
                JOIN canonical_effect_intents AS intent
                  ON intent.effect_intent_id = event.effect_intent_id
                WHERE {scopePredicate}
                ORDER BY event.event_id;
                """;
            command.Parameters.AddWithValue("$scope", scopeValue);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                EffectIntentIdentity identity = new(reader.GetString(0));
                if (!events.TryGetValue(identity, out List<EffectLifecycleEvent>? history))
                {
                    events[identity] = history = [];
                }
                history.Add(MapEvent(reader, offset: 1, identity));
            }
        }

        return [.. rows.Select(row => new EffectWorkItem(
            row.Intent, row.State,
            receipts.GetValueOrDefault(row.Identity),
            events.TryGetValue(row.Identity, out List<EffectLifecycleEvent>? history) ? history : []))];
    }

    private async Task<EffectReceipt?> ReadReceiptAsync(SqliteConnection connection, SqliteTransaction? transaction,
        string receiptId, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = CreateCommand(connection, transaction);
        command.CommandText = """
            SELECT receipt_id, effect_intent_id, executor_key, executor_version, observed_target_identity,
                   before_facts_json, after_facts_json, postcondition_satisfied, external_correlation,
                   evidence_json, recorded_at
            FROM canonical_effect_receipts WHERE receipt_id = $receipt;
            """;
        command.Parameters.AddWithValue("$receipt", receiptId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapReceipt(reader, offset: 0) : null;
    }

    private async Task<IReadOnlyList<EffectLifecycleEvent>> ReadEventsAsync(SqliteConnection connection,
        SqliteTransaction? transaction, EffectIntentIdentity identity, CancellationToken cancellationToken)
    {
        await using SqliteCommand command = CreateCommand(connection, transaction);
        command.CommandText = """
            SELECT event_id, lifecycle, worker_id, explanation, evidence_json, recorded_at
            FROM canonical_effect_lifecycle_events WHERE effect_intent_id = $intent ORDER BY event_id;
            """;
        command.Parameters.AddWithValue("$intent", identity.Value);
        var result = new List<EffectLifecycleEvent>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(MapEvent(reader, offset: 0, identity));
        return result;
    }

    /// <summary>
    /// Reads a receipt from <paramref name="offset"/>, shared by the item-at-a-time and set-based
    /// reads so the two cannot drift into observing the same row differently.
    /// </summary>
    private static EffectReceipt MapReceipt(SqliteDataReader reader, int offset) => new(
        new(reader.GetString(offset)), new(reader.GetString(offset + 1)), new(reader.GetString(offset + 2)),
        reader.GetString(offset + 3), reader.GetString(offset + 4), reader.GetString(offset + 5),
        reader.GetString(offset + 6), reader.GetBoolean(offset + 7),
        reader.IsDBNull(offset + 8) ? null : reader.GetString(offset + 8),
        JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(offset + 9), JsonOptions) ?? [],
        DateTimeOffset.Parse(reader.GetString(offset + 10), CultureInfo.InvariantCulture));

    // The event history is append-only, so a workspace written before the cut holds 'Leased' and
    // 'Started' event rows forever. ParseStatus is what keeps reading that history from throwing.
    private static EffectLifecycleEvent MapEvent(SqliteDataReader reader, int offset, EffectIntentIdentity identity) => new(
        reader.GetInt64(offset), identity, ParseStatus(reader.GetString(offset + 1)),
        reader.GetString(offset + 2), reader.GetString(offset + 3),
        JsonSerializer.Deserialize<IReadOnlyList<string>>(reader.GetString(offset + 4), JsonOptions) ?? [],
        DateTimeOffset.Parse(reader.GetString(offset + 5), CultureInfo.InvariantCulture));

    /// <summary>
    /// Appends a lifecycle event inside <paramref name="transaction"/> and returns the
    /// <c>AUTOINCREMENT</c> identifier the database assigned it. Event order is that identifier, so
    /// callers that project the appended event into a return value must take it from here rather
    /// than synthesise one.
    /// </summary>
    private static async Task<long> AppendEventAsync(SqliteConnection connection, SqliteTransaction transaction,
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
        await using SqliteCommand sequence = connection.CreateCommand();
        sequence.Transaction = transaction;
        sequence.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt64(await sequence.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
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
                   SUM(CASE WHEN intent.status = 'Succeeded' THEN 0 ELSE 1 END)
            FROM canonical_effect_intents AS intent
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

        // Routed from the typed fact `InterpretCompletionRoute` recorded, not from the prose it
        // rendered. The decision is the certification router's, and it reaches here as a decision;
        // recovering it by matching a row out of agent-authored markdown made routing depend on
        // that text's layout, which is neither the system's own fact nor stable.
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT document_json FROM canonical_transition_evidence
            WHERE run_id = $transition AND event_name = $event
            ORDER BY evidence_id DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$transition", transitionRun.Value);
        command.Parameters.AddWithValue("$event", CompletionRouteDecision.EventName);
        string? json = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        // Fail closed. An absent decision is not a licence to guess a successor, and it is never a
        // reason to fall back to reading the rendered output.
        CompletionRouteDecision decision = CompletionRouteDecision.FromDocumentJson(json)
            ?? throw new InvalidOperationException(
                $"Transition run '{transitionRun.Value}' carries no durable CompletionRoute decision " +
                $"('{CompletionRouteDecision.EventName}'), so the completion route cannot be resolved.");
        string successor = decision.ShouldCloseEpic ? "Workflow Completion" : "Execution Readiness";
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
