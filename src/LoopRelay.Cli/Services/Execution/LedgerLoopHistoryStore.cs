using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Services;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Cli.Services.Execution;

/// <summary>
/// SQLite implementation of the logical History Authority. A fact, its evidence set, and its
/// filesystem projection effect are committed atomically. Filesystem publication occurs later.
/// </summary>
internal sealed class LedgerLoopHistoryStore(Repository _repository) : ILoopHistoryStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

#if DEBUG
    /// <summary>
    /// Test-only observability: the read-only connection <see cref="ReadLatestAsync"/> opens is
    /// offered here immediately after it is opened, before any schema check or data read issues a
    /// statement on it. A test installs a SQLite authorizer on it (see
    /// <c>PreparedStatementCounter</c>) and counts the statements the read path really compiles,
    /// mirroring the seam <c>CanonicalEffectWorkStore.ConnectionObserverForTesting</c> already uses
    /// for the same reason: Microsoft.Data.Sqlite exposes no statement hook, and this store opens
    /// its own connections with pooling disabled, so a test cannot otherwise reach the handle a
    /// read ran on.
    /// </summary>
    internal Action<SqliteConnection>? ConnectionObserverForTesting { get; set; }
#endif

    public async Task<LoopHistoryRecord> AppendAsync(
        LoopHistoryAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        LoopHistorySpec spec = GetSpec(request.Kind);
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        string workspaceId = await LoopRelayWorkspaceDatabase.ReadWorkspaceIdentityAsync(connection, cancellationToken);
        if (!string.Equals(workspaceId, request.Causality.Workspace.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "History fact causality belongs to a different workspace identity.");
        }

        // Rotation is a convergence, not an accumulation - but only against the LATEST fact.
        // A crash between this append committing and the rotation executor deleting its live
        // source leaves both, so the re-executed effect reads byte-identical content and appends
        // again; because that retry happens before any newer same-kind fact can be produced, the
        // already-committed fact is still the latest and the retry converges on it, minting no
        // second identity, evidence set, or projection effect. Content that legitimately recurs
        // AFTER an intervening fact must append fresh: converging across history would silently
        // resurface an old fact while the caller deletes the live file, making "latest" lie.
        // The check runs inside the write transaction (IMMEDIATE by default in
        // Microsoft.Data.Sqlite), so two racing writers serialize; no unique-index backstop is
        // needed - or possible, since "unique against latest" is not expressible as an index.
        string contentHash = LoopHistoryRecord.ComputeContentHash(request.Content);
        await using SqliteTransaction transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        LatestRow? latest = await ReadLatestRowAsync(connection, transaction, spec, cancellationToken);
        if (latest is { HistoryId: not null } converged &&
            string.Equals(converged.ContentHash, contentHash, StringComparison.Ordinal))
        {
            await transaction.RollbackAsync(cancellationToken);
            return await MaterializeRecordAsync(connection, converged, request.Kind, cancellationToken);
        }

        long sequence = (latest?.Sequence ?? 0) + 1;
        HistoryFactIdentity historyIdentity = HistoryFactIdentity.New();
        string relativePath = spec.HistoricalPath(checked((int)sequence));
        DateTimeOffset recordedAt = DateTimeOffset.UtcNow;
        await InsertFactAsync(
            connection,
            transaction,
            historyIdentity,
            spec,
            sequence,
            relativePath,
            request,
            contentHash,
            recordedAt,
            cancellationToken);
        await InsertEvidenceAsync(
            connection,
            transaction,
            historyIdentity,
            request.Evidence,
            recordedAt,
            cancellationToken);
        await InsertProjectionEffectAsync(
            connection,
            transaction,
            historyIdentity,
            request,
            relativePath,
            contentHash,
            recordedAt,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new LoopHistoryRecord(
            historyIdentity,
            request.Kind,
            sequence,
            recordedAt,
            request.Content,
            contentHash,
            request.Causality,
            request.Evidence,
            request.Supersedes,
            relativePath);
    }

    public async Task<LoopHistoryRecord?> ReadLatestAsync(
        LoopHistoryKind kind,
        CancellationToken cancellationToken = default)
    {
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(_repository);
        if (!File.Exists(databasePath))
        {
            return null;
        }

        LoopHistorySpec spec = GetSpec(kind);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync(cancellationToken);
#if DEBUG
        ConnectionObserverForTesting?.Invoke(connection);
#endif
        // Bound to the schema-admission memo instead of re-proving structure on every read: once
        // this process has admitted the database once (EnsureSchemaAsync, on any connection to the
        // same file), InspectMemoizedAsync answers from that memo plus a cheap live stamp re-check
        // (2 SELECTs) instead of the ~190-probe InspectSchemaAsync inspection. It returns null -
        // and full InspectSchemaAsync classification takes over - whenever the memo is cold or the
        // live stamp no longer matches it, so a tampered or not-yet-admitted database is still
        // classified in full and still fails closed exactly as before. InspectStampedAsync is
        // deliberately not used as this fallback: by its own documented trade-off it trusts the
        // on-disk stamp over physical shape, so a store whose stamp is intact but whose physical
        // structure has been corrupted out-of-band would pass here instead of being rejected.
        WorkspaceSchemaInspection inspection =
            await LoopRelayWorkspaceDatabase.InspectMemoizedAsync(connection, cancellationToken)
            ?? await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection, cancellationToken);
        if (inspection.Family != WorkspaceSchemaFamily.CanonicalWorkspace ||
            inspection.Version != LoopRelayWorkspaceDatabase.CurrentSchemaVersion)
        {
            throw new WorkspaceCompatibilityImportRequiredException(inspection);
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT history_id, sequence, logical_path, body, content_hash, created_at,
                   workspace_id, run_id, workflow_instance_id, transition_run_id, attempt_id,
                   session_id, turn_id, supersedes_id
            FROM loop_history
            WHERE kind = $kind
            ORDER BY sequence DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$kind", spec.KindToken);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        if (reader.IsDBNull(0) || reader.IsDBNull(6) || reader.IsDBNull(7) || reader.IsDBNull(8) ||
            reader.IsDBNull(9) || reader.IsDBNull(10))
        {
            throw new InvalidOperationException(
                "Legacy history rows require explicit Compatibility Authority import before canonical reads.");
        }

        HistoryFactIdentity identity = new(reader.GetString(0));
        long sequence = reader.GetInt64(1);
        string relativePath = reader.GetString(2);
        string content = reader.GetString(3);
        string contentHash = reader.GetString(4);
        DateTimeOffset recordedAt = DateTimeOffset.Parse(
            reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var causality = new CanonicalCausalContext(
            new WorkspaceIdentity(reader.GetString(6)),
            new RunIdentity(reader.GetString(7)),
            new WorkflowInstanceIdentity(reader.GetString(8)),
            new TransitionRunIdentity(reader.GetString(9)),
            new AttemptIdentity(reader.GetString(10)),
            reader.IsDBNull(11) ? null : new AgentSessionIdentity(reader.GetString(11)),
            reader.IsDBNull(12) ? null : new TurnIdentity(reader.GetString(12)));
        HistoryEvidenceAttachments evidence = await ReadEvidenceAsync(
            connection, identity, cancellationToken);
        return new LoopHistoryRecord(
            identity,
            kind,
            sequence,
            recordedAt,
            content,
            contentHash,
            causality,
            evidence,
            reader.IsDBNull(13) ? null : new HistoryFactIdentity(reader.GetString(13)),
            relativePath);
    }

    /// <summary>
    /// Latest row of a kind, canonical or legacy, read inside the write transaction. Legacy rows
    /// (null history_id) never converge - they carry no canonical causality to answer with - but
    /// still participate in sequence derivation exactly as the old MAX(sequence) query did.
    /// </summary>
    private static async Task<LatestRow?> ReadLatestRowAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        LoopHistorySpec spec,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT history_id, sequence, logical_path, body, content_hash, created_at,
                   workspace_id, run_id, workflow_instance_id, transition_run_id, attempt_id,
                   session_id, turn_id, supersedes_id
            FROM loop_history
            WHERE kind = $kind
            ORDER BY sequence DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$kind", spec.KindToken);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LatestRow(
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.GetInt64(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13));
    }

    /// <summary>
    /// Builds the converged record. Evidence is read after the transaction has been rolled back
    /// (a read-only transaction rolls back harmlessly) because <see cref="ReadEvidenceAsync"/>
    /// creates its own commands on this connection.
    /// </summary>
    private static async Task<LoopHistoryRecord> MaterializeRecordAsync(
        SqliteConnection connection,
        LatestRow row,
        LoopHistoryKind kind,
        CancellationToken cancellationToken)
    {
        HistoryFactIdentity identity = new(row.HistoryId!);
        HistoryEvidenceAttachments evidence = await ReadEvidenceAsync(connection, identity, cancellationToken);
        return new LoopHistoryRecord(
            identity,
            kind,
            row.Sequence,
            DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            row.Body,
            row.ContentHash,
            new CanonicalCausalContext(
                new WorkspaceIdentity(row.WorkspaceId!),
                new RunIdentity(row.RunId!),
                new WorkflowInstanceIdentity(row.WorkflowInstanceId!),
                new TransitionRunIdentity(row.TransitionRunId!),
                new AttemptIdentity(row.AttemptId!),
                row.SessionId is null ? null : new AgentSessionIdentity(row.SessionId),
                row.TurnId is null ? null : new TurnIdentity(row.TurnId)),
            evidence,
            row.SupersedesId is null ? null : new HistoryFactIdentity(row.SupersedesId),
            row.LogicalPath);
    }

    private sealed record LatestRow(
        string? HistoryId,
        long Sequence,
        string LogicalPath,
        string Body,
        string ContentHash,
        string CreatedAt,
        string? WorkspaceId,
        string? RunId,
        string? WorkflowInstanceId,
        string? TransitionRunId,
        string? AttemptId,
        string? SessionId,
        string? TurnId,
        string? SupersedesId);

    private static async Task InsertFactAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HistoryFactIdentity identity,
        LoopHistorySpec spec,
        long sequence,
        string relativePath,
        LoopHistoryAppendRequest request,
        string contentHash,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO loop_history (
                kind, sequence, logical_path, body, content_hash, created_at, history_id,
                workspace_id, run_id, workflow_instance_id, transition_run_id, attempt_id,
                session_id, turn_id, supersedes_id
            )
            VALUES (
                $kind, $sequence, $logical_path, $body, $content_hash, $created_at, $history_id,
                $workspace_id, $run_id, $workflow_instance_id, $transition_run_id, $attempt_id,
                $session_id, $turn_id, $supersedes_id
            );
            """;
        command.Parameters.AddWithValue("$kind", spec.KindToken);
        command.Parameters.AddWithValue("$sequence", sequence);
        command.Parameters.AddWithValue("$logical_path", relativePath);
        command.Parameters.AddWithValue("$body", request.Content);
        command.Parameters.AddWithValue("$content_hash", contentHash);
        command.Parameters.AddWithValue("$created_at", recordedAt.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$history_id", identity.Value);
        command.Parameters.AddWithValue("$workspace_id", request.Causality.Workspace.Value);
        command.Parameters.AddWithValue("$run_id", request.Causality.Run.Value);
        command.Parameters.AddWithValue("$workflow_instance_id", request.Causality.WorkflowInstance.Value);
        command.Parameters.AddWithValue("$transition_run_id", request.Causality.TransitionRun.Value);
        command.Parameters.AddWithValue("$attempt_id", request.Causality.Attempt.Value);
        command.Parameters.AddWithValue("$session_id", (object?)request.Causality.Session?.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("$turn_id", (object?)request.Causality.Turn?.Value ?? DBNull.Value);
        command.Parameters.AddWithValue("$supersedes_id", (object?)request.Supersedes?.Value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertEvidenceAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HistoryFactIdentity historyIdentity,
        HistoryEvidenceAttachments evidence,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        string evidenceSetId = evidence.Identity.Value;
        await ExecuteAsync(
            connection,
            transaction,
            "INSERT INTO history_evidence_sets (evidence_set_id, history_id, created_at) VALUES ($set, $history, $at);",
            cancellationToken,
            ("$set", evidenceSetId),
            ("$history", historyIdentity.Value),
            ("$at", recordedAt.ToString("O", CultureInfo.InvariantCulture)));

        if (evidence.Provider is not null)
        {
            await InsertEvidenceItemAsync(connection, transaction, evidenceSetId, evidence.Provider.Identity, "Provider", evidence.Provider,
                evidence.Provider.Provider, evidence.Provider.ProviderThreadId, evidence.Provider.ProviderTurnId,
                null, null, null, null, recordedAt, cancellationToken);
        }

        if (evidence.Continuity is not null)
        {
            await InsertEvidenceItemAsync(connection, transaction, evidenceSetId, evidence.Continuity.Identity, "Continuity", evidence.Continuity,
                null, null, null, evidence.Continuity.Lineage.Value, null, null, null, recordedAt, cancellationToken);
        }

        if (evidence.Recovery is not null)
        {
            await InsertEvidenceItemAsync(connection, transaction, evidenceSetId, evidence.Recovery.Identity, "Recovery", evidence.Recovery,
                null, null, null, null, evidence.Recovery.RecoveryAttempt.Value, null, null, recordedAt, cancellationToken);
        }

        if (evidence.Repository is not null)
        {
            await InsertEvidenceItemAsync(connection, transaction, evidenceSetId, evidence.Repository.Identity, "Repository", evidence.Repository,
                null, null, null, null, null, evidence.Repository.CommitHash, null, recordedAt, cancellationToken);
        }

        if (evidence.Effects is not null)
        {
            string? effectIdentity = evidence.Effects.EffectIdentities.Count == 1
                ? evidence.Effects.EffectIdentities[0]
                : null;
            await InsertEvidenceItemAsync(connection, transaction, evidenceSetId, evidence.Effects.Identity, "Effect", evidence.Effects,
                null, null, null, null, null, null, effectIdentity, recordedAt, cancellationToken);
        }
    }

    private static Task InsertEvidenceItemAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string evidenceSetId,
        HistoryEvidenceItemIdentity evidenceItemIdentity,
        string kind,
        object payload,
        string? provider,
        string? providerThread,
        string? providerTurn,
        string? continuityLineage,
        string? recoveryAttempt,
        string? repositoryCommit,
        string? effectIdentity,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO history_evidence_items (
                evidence_item_id, evidence_set_id, evidence_kind, schema_version,
                provider, provider_thread_id, provider_turn_id, continuity_lineage_id,
                recovery_attempt_id, repository_commit, effect_identity, payload_json, recorded_at
            )
            VALUES (
                $item, $set, $kind, 'history-evidence-v1',
                $provider, $provider_thread, $provider_turn, $continuity,
                $recovery, $commit, $effect, $payload, $at
            );
            """,
            cancellationToken,
            ("$item", evidenceItemIdentity.Value),
            ("$set", evidenceSetId),
            ("$kind", kind),
            ("$provider", provider),
            ("$provider_thread", providerThread),
            ("$provider_turn", providerTurn),
            ("$continuity", continuityLineage),
            ("$recovery", recoveryAttempt),
            ("$commit", repositoryCommit),
            ("$effect", effectIdentity),
            ("$payload", JsonSerializer.Serialize(payload, payload.GetType(), Json)),
            ("$at", recordedAt.ToString("O", CultureInfo.InvariantCulture)));

    private static async Task InsertProjectionEffectAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        HistoryFactIdentity historyIdentity,
        LoopHistoryAppendRequest request,
        string relativePath,
        string contentHash,
        DateTimeOffset plannedAt,
        CancellationToken cancellationToken)
    {
        EffectParent? parent = request.Parent;
        var payload = new FilesystemWriteEffectPayload(relativePath, request.Content);
        string payloadJson = JsonSerializer.Serialize(payload, Json);
        var intent = new EffectIntent(
            EffectIntentIdentity.New(),
            request.Causality,
            $"history:materialize:{request.Kind}",
            WorkspaceEffectExecutorKeys.FilesystemWrite,
            "1",
            new EffectTargetDescriptor(
                "HistoryProjection",
                relativePath,
                JsonSerializer.Serialize(new { historyId = historyIdentity.Value, contentHash }, Json)),
            payloadJson,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson))),
            parent?.Order + 1 ?? 0,
            parent is null ? [] : [parent.Identity],
            EffectRequiredness.BlockingLocal,
            new EffectCondition("history-fact-durable", JsonSerializer.Serialize(new { historyId = historyIdentity.Value }, Json)),
            new EffectCondition("content-hash", JsonSerializer.Serialize(new { relativePath, contentHash }, Json)),
            "independent-content-hash",
            $"history-projection:{historyIdentity.Value}:{relativePath}:{contentHash}",
            plannedAt);
        await ExecuteAsync(
            connection,
            transaction,
            """
            INSERT INTO canonical_effect_intents (
                effect_intent_id, transition_run_id, attempt_id, effect_identity, category,
                effect_order, idempotency_key, status, definition_json, planned_at,
                workspace_id, run_id, workflow_instance_id, semantic_operation_key,
                executor_key, executor_version, target_json, payload_json, payload_hash,
                requiredness, dependencies_json, precondition_json, postcondition_json,
                reconciliation_policy
            ) VALUES (
                $intent, $transition, $attempt, $effect_identity, 'Archive',
                $order, $key, 'Planned', $definition, $at,
                $workspace, $run, $workflow_instance, $semantic,
                $executor, $executor_version, $target, $payload, $payload_hash,
                $requiredness, $dependencies, $precondition, $postcondition,
                $reconciliation
            );
            INSERT INTO canonical_effect_lifecycle_events (
                effect_intent_id, lifecycle, worker_id, explanation, evidence_json, recorded_at
            ) VALUES (
                $intent, 'Planned', 'history-authority',
                'History projection enqueued atomically with its canonical history fact.',
                $evidence, $at
            );
            """,
            cancellationToken,
            ("$intent", intent.Identity.Value),
            ("$transition", intent.Causality.TransitionRun.Value),
            ("$attempt", intent.Causality.Attempt.Value),
            ("$effect_identity", $"history-projection:{historyIdentity.Value}"),
            ("$order", intent.Order),
            ("$key", intent.IdempotencyKey),
            ("$definition", JsonSerializer.Serialize(intent, Json)),
            ("$at", plannedAt.ToString("O", CultureInfo.InvariantCulture)),
            ("$workspace", intent.Causality.Workspace.Value),
            ("$run", intent.Causality.Run.Value),
            ("$workflow_instance", intent.Causality.WorkflowInstance.Value),
            ("$semantic", intent.SemanticOperationKey),
            ("$executor", intent.Executor.Value),
            ("$executor_version", intent.ExecutorVersion),
            ("$target", JsonSerializer.Serialize(intent.Target, Json)),
            ("$payload", intent.TypedPayload),
            ("$payload_hash", intent.TypedPayloadHash),
            ("$requiredness", intent.Requiredness.ToString()),
            ("$dependencies", JsonSerializer.Serialize(intent.Dependencies, Json)),
            ("$precondition", JsonSerializer.Serialize(intent.Precondition, Json)),
            ("$postcondition", JsonSerializer.Serialize(intent.Postcondition, Json)),
            ("$reconciliation", intent.ReconciliationPolicy),
            ("$evidence", JsonSerializer.Serialize(new[] { historyIdentity.Value, relativePath }, Json)));
    }

    private static async Task<HistoryEvidenceAttachments> ReadEvidenceAsync(
        SqliteConnection connection,
        HistoryFactIdentity historyIdentity,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT set_row.evidence_set_id, item.evidence_item_id, item.evidence_kind, item.payload_json
            FROM history_evidence_sets set_row
            LEFT JOIN history_evidence_items item ON item.evidence_set_id = set_row.evidence_set_id
            WHERE set_row.history_id = $history
            ORDER BY item.recorded_at, item.evidence_item_id;
            """;
        command.Parameters.AddWithValue("$history", historyIdentity.Value);
        HistoryEvidenceSetIdentity? evidenceSetIdentity = null;
        HistoryProviderEvidence? provider = null;
        HistoryContinuityEvidence? continuity = null;
        HistoryRecoveryEvidence? recovery = null;
        HistoryRepositoryEvidence? repository = null;
        HistoryEffectEvidence? effects = null;
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            evidenceSetIdentity ??= new HistoryEvidenceSetIdentity(reader.GetString(0));
            if (reader.IsDBNull(1))
            {
                continue;
            }

            HistoryEvidenceItemIdentity storedItemIdentity = new(reader.GetString(1));
            string kind = reader.GetString(2);
            string payload = reader.GetString(3);
            switch (kind)
            {
                case "Provider": provider ??= DeserializeEvidence<HistoryProviderEvidence>(payload, storedItemIdentity); break;
                case "Continuity": continuity ??= DeserializeEvidence<HistoryContinuityEvidence>(payload, storedItemIdentity); break;
                case "Recovery": recovery ??= DeserializeEvidence<HistoryRecoveryEvidence>(payload, storedItemIdentity); break;
                case "Repository": repository ??= DeserializeEvidence<HistoryRepositoryEvidence>(payload, storedItemIdentity); break;
                case "Effect": effects ??= DeserializeEvidence<HistoryEffectEvidence>(payload, storedItemIdentity); break;
            }
        }

        return evidenceSetIdentity is { } identity
            ? new HistoryEvidenceAttachments(identity, provider, continuity, recovery, repository, effects)
            : HistoryEvidenceAttachments.Empty;
    }

    private static T DeserializeEvidence<T>(string payload, HistoryEvidenceItemIdentity storedIdentity)
        where T : class
    {
        T evidence = JsonSerializer.Deserialize<T>(payload, Json)
            ?? throw new InvalidDataException($"History evidence payload for '{typeof(T).Name}' is null.");
        HistoryEvidenceItemIdentity payloadIdentity = evidence switch
        {
            HistoryProviderEvidence item => item.Identity,
            HistoryContinuityEvidence item => item.Identity,
            HistoryRecoveryEvidence item => item.Identity,
            HistoryRepositoryEvidence item => item.Identity,
            HistoryEffectEvidence item => item.Identity,
            _ => throw new InvalidDataException($"Unsupported history evidence payload type '{typeof(T).Name}'.")
        };
        if (payloadIdentity != storedIdentity)
        {
            throw new InvalidDataException(
                $"History evidence identity mismatch: row '{storedIdentity}' does not match payload '{payloadIdentity}'.");
        }

        return evidence;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static LoopHistorySpec GetSpec(LoopHistoryKind kind) => kind switch
    {
        LoopHistoryKind.Decisions => new("Decisions", OrchestrationArtifactPaths.HistoricalDecision),
        LoopHistoryKind.Handoff => new("Handoff", OrchestrationArtifactPaths.HistoricalHandoff),
        LoopHistoryKind.OperationalDelta => new("OperationalDelta", OrchestrationArtifactPaths.HistoricalDelta),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private readonly record struct LoopHistorySpec(
        string KindToken,
        Func<int, string> HistoricalPath);
}
