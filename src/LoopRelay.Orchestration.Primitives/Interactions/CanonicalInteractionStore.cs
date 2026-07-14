using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Interactions;

public sealed class CanonicalInteractionStore(Repository _repository) : IInteractionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<InteractionAggregate?> ReadAsync(
        InteractionRequestIdentity request,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        return await ReadCoreAsync(connection, transaction: null, request, cancellationToken);
    }

    public async Task<IReadOnlyList<InteractionAggregate>> ListAsync(
        bool outstandingOnly,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = outstandingOnly
            ? """
              SELECT request_id FROM canonical_interaction_requests
              WHERE current_state NOT IN ('ResumeAuthorized', 'Cancelled', 'Expired', 'Defaulted')
              ORDER BY created_at, request_id;
              """
            : "SELECT request_id FROM canonical_interaction_requests ORDER BY created_at, request_id;";
        var identities = new List<InteractionRequestIdentity>();
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                identities.Add(new InteractionRequestIdentity(reader.GetString(0)));
        }

        var result = new List<InteractionAggregate>(identities.Count);
        foreach (InteractionRequestIdentity identity in identities)
        {
            InteractionAggregate? aggregate = await ReadCoreAsync(connection, null, identity, cancellationToken);
            if (aggregate is not null) result.Add(aggregate);
        }
        return result;
    }

    public async Task<InteractionAggregate?> ReadBySemanticIdempotencyKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT request_id FROM canonical_interaction_requests WHERE semantic_idempotency_key = $key;";
        command.Parameters.AddWithValue("$key", key);
        object? scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is null or DBNull
            ? null
            : await ReadCoreAsync(connection, null, new InteractionRequestIdentity((string)scalar), cancellationToken);
    }

    public async Task PersistRequestAsync(
        InteractionRequest request,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await using (SqliteCommand policy = connection.CreateCommand())
        {
            policy.Transaction = transaction;
            policy.CommandText = """
                INSERT INTO canonical_interaction_policy_evaluations (
                    policy_evaluation_id, category, question_version, response_schema_version,
                    response_json_schema, response_schema_hash, deadline_behavior, deadline,
                    default_response_json, headless_outcome, required_trust_evidence_json,
                    resolver_owner, resolved_policy_identity, evaluated_at
                ) VALUES (
                    $identity, $category, $question_version, $schema_version, $schema, $schema_hash,
                    $deadline_behavior, $deadline, $default, $headless, $trust, $owner, $resolved, $evaluated
                );
                """;
            Add(policy,
                ("$identity", request.Policy.Identity.Value), ("$category", request.Policy.Category.ToString()),
                ("$question_version", request.Policy.QuestionVersion),
                ("$schema_version", request.Policy.ResponseSchemaVersion),
                ("$schema", request.Policy.ResponseJsonSchema), ("$schema_hash", request.Policy.ResponseSchemaHash),
                ("$deadline_behavior", request.Policy.DeadlineBehavior.ToString()),
                ("$deadline", Format(request.Policy.Deadline)), ("$default", request.Policy.DefaultResponseJson),
                ("$headless", request.Policy.HeadlessOutcome.ToString()),
                ("$trust", Serialize(request.Policy.RequiredTrustEvidence)),
                ("$owner", request.Policy.ResolverOwner), ("$resolved", request.Policy.ResolvedPolicyIdentity),
                ("$evaluated", Format(request.Policy.EvaluatedAt)));
            await policy.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO canonical_interaction_requests (
                    request_id, category, workspace_id, run_id, workflow_instance_id,
                    transition_run_id, attempt_id, subject_json, question, presentation_json,
                    policy_evaluation_id, creation_evidence_json, semantic_idempotency_key,
                    current_state, row_version, created_at
                ) VALUES (
                    $identity, $category, $workspace, $run, $workflow, $transition, $attempt,
                    $subject, $question, $presentation, $policy, $evidence, $idempotency,
                    'Persisted', 1, $created
                );
                """;
            Add(insert,
                ("$identity", request.Identity.Value), ("$category", request.Category.ToString()),
                ("$workspace", request.Subject.Causality.Workspace.Value),
                ("$run", request.Subject.Causality.Run.Value),
                ("$workflow", request.Subject.Causality.WorkflowInstance.Value),
                ("$transition", request.Subject.Causality.TransitionRun.Value),
                ("$attempt", request.Subject.Causality.Attempt.Value),
                ("$subject", JsonSerializer.Serialize(request.Subject, JsonOptions)),
                ("$question", request.Question), ("$presentation", request.PresentationJson),
                ("$policy", request.Policy.Identity.Value), ("$evidence", Serialize(request.CreationEvidence)),
                ("$idempotency", request.SemanticIdempotencyKey), ("$created", Format(request.CreatedAt)));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(connection, transaction, new InteractionLifecycleEvent(
            InteractionEventIdentity.New(), request.Identity, InteractionLifecycle.Persisted,
            "Interaction request was durably persisted before presentation.", request.CreationEvidence,
            request.CreatedAt), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<InteractionAggregate> AppendEventAsync(
        InteractionLifecycleEvent lifecycleEvent,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        InteractionAggregate aggregate = await ReadCoreAsync(
            connection, transaction, lifecycleEvent.Request, cancellationToken)
            ?? throw new KeyNotFoundException($"Interaction request `{lifecycleEvent.Request}` was not found.");
        if (aggregate.RowVersion != expectedRowVersion)
            throw new InvalidOperationException("Interaction compare-and-set conflict.");

        InteractionLifecycle next = ValidateTransition(aggregate.State, lifecycleEvent.Lifecycle);
        await InsertEventAsync(connection, transaction, lifecycleEvent, cancellationToken);
        await UpdateStateAsync(connection, transaction, aggregate.Request.Identity, aggregate.RowVersion,
            next, increment: 1, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await ReadAsync(lifecycleEvent.Request, cancellationToken))!;
    }

    public async Task<InteractionResponseResult> TryAcceptResponseAsync(
        InteractionResponse response,
        long expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        InteractionAggregate? aggregate = await ReadCoreAsync(connection, transaction, response.Request, cancellationToken);
        if (aggregate is null)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Rejected(InteractionRejectionReason.NotFound, "Interaction request was not found.", null);
        }

        if (aggregate.AcceptedResponse is { } accepted)
        {
            bool identical = string.Equals(
                accepted.SemanticResponseHash,
                response.SemanticResponseHash,
                StringComparison.Ordinal);
            if (identical)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new InteractionResponseResult(true, true, accepted, null,
                    "Semantically identical response was already accepted.", aggregate);
            }
            InteractionAggregate rejected = await AppendRejectionAsync(connection, transaction, aggregate,
                expectedRowVersion, InteractionRejectionReason.SemanticIdempotencyConflict, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Rejected(InteractionRejectionReason.SemanticIdempotencyConflict,
                "A conflicting response cannot replace the accepted response.", rejected);
        }

        if (aggregate.RowVersion != expectedRowVersion)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Rejected(InteractionRejectionReason.CompareAndSetConflict,
                "Interaction compare-and-set conflict.", aggregate);
        }
        if (aggregate.State != InteractionLifecycle.Presented)
        {
            InteractionRejectionReason reason = aggregate.State switch
            {
                InteractionLifecycle.Cancelled => InteractionRejectionReason.Cancelled,
                InteractionLifecycle.Expired or InteractionLifecycle.Defaulted => InteractionRejectionReason.Expired,
                _ => InteractionRejectionReason.AlreadyResolvedConflict,
            };
            InteractionAggregate rejected = await AppendRejectionAsync(
                connection, transaction, aggregate, expectedRowVersion, reason, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Rejected(reason, $"Interaction in state `{aggregate.State}` cannot accept a response.", rejected);
        }

        await using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO canonical_interaction_responses (
                    response_id, request_id, response_json, semantic_response_hash,
                    semantic_idempotency_key, trust_evidence_json, responder_identity, responded_at
                ) VALUES ($identity, $request, $json, $hash, $idempotency, $trust, $responder, $responded);
                """;
            Add(insert,
                ("$identity", response.Identity.Value), ("$request", response.Request.Value),
                ("$json", response.ResponseJson), ("$hash", response.SemanticResponseHash),
                ("$idempotency", response.SemanticIdempotencyKey),
                ("$trust", Serialize(response.TrustEvidence)), ("$responder", response.ResponderIdentity),
                ("$responded", Format(response.RespondedAt)));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertEventAsync(connection, transaction, new InteractionLifecycleEvent(
            InteractionEventIdentity.New(), response.Request, InteractionLifecycle.Responded,
            "Immutable interaction response was accepted.", [response.Identity.Value], response.RespondedAt),
            cancellationToken);
        await InsertEventAsync(connection, transaction, new InteractionLifecycleEvent(
            InteractionEventIdentity.New(), response.Request, InteractionLifecycle.Validated,
            "Interaction response satisfied schema, state, trust, correlation, and compare-and-set checks.",
            [response.Identity.Value, response.SemanticResponseHash], response.RespondedAt), cancellationToken);
        await UpdateStateAsync(connection, transaction, response.Request, aggregate.RowVersion,
            InteractionLifecycle.Validated, increment: 2, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        InteractionAggregate updated = (await ReadAsync(response.Request, cancellationToken))!;
        return new InteractionResponseResult(true, false, response, null,
            "Interaction response was accepted and validated.", updated);
    }

    private static async Task<InteractionAggregate> AppendRejectionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        InteractionAggregate aggregate,
        long expectedRowVersion,
        InteractionRejectionReason reason,
        CancellationToken cancellationToken)
    {
        if (aggregate.RowVersion != expectedRowVersion)
            return aggregate;
        await InsertEventAsync(connection, transaction, new InteractionLifecycleEvent(
            InteractionEventIdentity.New(), aggregate.Request.Identity, InteractionLifecycle.Rejected,
            $"Interaction response rejected: {reason}.", [reason.ToString()], DateTimeOffset.UtcNow), cancellationToken);
        await UpdateStateAsync(connection, transaction, aggregate.Request.Identity, aggregate.RowVersion,
            aggregate.State, increment: 1, cancellationToken);
        return (await ReadCoreAsync(connection, transaction, aggregate.Request.Identity, cancellationToken))!;
    }

    private static InteractionLifecycle ValidateTransition(InteractionLifecycle current, InteractionLifecycle requested)
    {
        if (requested == InteractionLifecycle.Rejected) return current;
        if (requested == InteractionLifecycle.Presented && current is InteractionLifecycle.Persisted or InteractionLifecycle.Presented)
            return InteractionLifecycle.Presented;
        if (requested is InteractionLifecycle.Expired or InteractionLifecycle.Defaulted or InteractionLifecycle.Cancelled &&
            current is InteractionLifecycle.Persisted or InteractionLifecycle.Presented)
            return requested;
        if (requested == InteractionLifecycle.Resolved && current == InteractionLifecycle.Validated)
            return requested;
        if (requested == InteractionLifecycle.ResumeAuthorized && current == InteractionLifecycle.Resolved)
            return requested;
        throw new InvalidOperationException($"Invalid interaction lifecycle transition `{current}` -> `{requested}`.");
    }

    private static async Task UpdateStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        InteractionRequestIdentity request,
        long expectedRowVersion,
        InteractionLifecycle state,
        int increment,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
            UPDATE canonical_interaction_requests
            SET current_state = $state, row_version = row_version + $increment
            WHERE request_id = $request AND row_version = $expected;
            """;
        Add(update, ("$state", state.ToString()), ("$increment", increment),
            ("$request", request.Value), ("$expected", expectedRowVersion));
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Interaction compare-and-set conflict.");
    }

    private static async Task InsertEventAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        InteractionLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO canonical_interaction_lifecycle_events (
                event_identity, request_id, lifecycle, explanation, evidence_json, recorded_at
            ) VALUES ($identity, $request, $lifecycle, $explanation, $evidence, $recorded);
            """;
        Add(command,
            ("$identity", lifecycleEvent.Identity.Value), ("$request", lifecycleEvent.Request.Value),
            ("$lifecycle", lifecycleEvent.Lifecycle.ToString()), ("$explanation", lifecycleEvent.Explanation),
            ("$evidence", Serialize(lifecycleEvent.Evidence)), ("$recorded", Format(lifecycleEvent.RecordedAt)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<InteractionAggregate?> ReadCoreAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        InteractionRequestIdentity request,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT request.category, request.subject_json, request.question, request.presentation_json,
                   request.creation_evidence_json, request.semantic_idempotency_key, request.created_at,
                   request.current_state, request.row_version,
                   policy.policy_evaluation_id, policy.category, policy.question_version,
                   policy.response_schema_version, policy.response_json_schema, policy.response_schema_hash,
                   policy.deadline_behavior, policy.deadline, policy.default_response_json,
                   policy.headless_outcome, policy.required_trust_evidence_json, policy.resolver_owner,
                   policy.resolved_policy_identity, policy.evaluated_at
            FROM canonical_interaction_requests AS request
            JOIN canonical_interaction_policy_evaluations AS policy
              ON policy.policy_evaluation_id = request.policy_evaluation_id
            WHERE request.request_id = $request;
            """;
        command.Parameters.AddWithValue("$request", request.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        InteractionCategory category = Parse<InteractionCategory>(reader.GetString(0));
        InteractionCausalSubject subject = Deserialize<InteractionCausalSubject>(reader.GetString(1));
        var policy = new InteractionCategoryPolicy(
            new InteractionPolicyEvaluationIdentity(reader.GetString(9)), Parse<InteractionCategory>(reader.GetString(10)),
            reader.GetString(11), reader.GetString(12), reader.GetString(13), reader.GetString(14),
            Parse<InteractionDeadlineBehavior>(reader.GetString(15)),
            reader.IsDBNull(16) ? null : ParseDate(reader.GetString(16)),
            reader.IsDBNull(17) ? null : reader.GetString(17), Parse<RuntimeOutcomeKind>(reader.GetString(18)),
            DeserializeList(reader.GetString(19)), reader.GetString(20), reader.GetString(21), ParseDate(reader.GetString(22)));
        var interactionRequest = new InteractionRequest(
            request, category, subject, reader.GetString(2), reader.GetString(3), policy,
            DeserializeList(reader.GetString(4)), reader.GetString(5), ParseDate(reader.GetString(6)));
        InteractionLifecycle state = Parse<InteractionLifecycle>(reader.GetString(7));
        long rowVersion = reader.GetInt64(8);
        await reader.DisposeAsync();
        InteractionResponse? response = await ReadResponseAsync(connection, transaction, request, cancellationToken);
        IReadOnlyList<InteractionLifecycleEvent> events = await ReadEventsAsync(connection, transaction, request, cancellationToken);
        return new InteractionAggregate(interactionRequest, state, rowVersion, response, events);
    }

    private static async Task<InteractionResponse?> ReadResponseAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        InteractionRequestIdentity request,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT response_id, response_json, semantic_response_hash, semantic_idempotency_key,
                   trust_evidence_json, responder_identity, responded_at
            FROM canonical_interaction_responses WHERE request_id = $request;
            """;
        command.Parameters.AddWithValue("$request", request.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new InteractionResponse(new InteractionResponseIdentity(reader.GetString(0)), request,
                reader.GetString(1), reader.GetString(2), reader.GetString(3), DeserializeList(reader.GetString(4)),
                reader.GetString(5), ParseDate(reader.GetString(6)))
            : null;
    }

    private static async Task<IReadOnlyList<InteractionLifecycleEvent>> ReadEventsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        InteractionRequestIdentity request,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT event_identity, lifecycle, explanation, evidence_json, recorded_at
            FROM canonical_interaction_lifecycle_events WHERE request_id = $request ORDER BY event_id;
            """;
        command.Parameters.AddWithValue("$request", request.Value);
        var result = new List<InteractionLifecycleEvent>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new InteractionLifecycleEvent(new InteractionEventIdentity(reader.GetString(0)), request,
                Parse<InteractionLifecycle>(reader.GetString(1)), reader.GetString(2),
                DeserializeList(reader.GetString(3)), ParseDate(reader.GetString(4))));
        return result;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        string path = LoopRelayWorkspaceDatabase.Resolve(_repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(path);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        return connection;
    }

    private static InteractionResponseResult Rejected(
        InteractionRejectionReason reason,
        string explanation,
        InteractionAggregate? aggregate) => new(false, false, null, reason, explanation, aggregate);

    private static void Add(SqliteCommand command, params (string Name, object? Value)[] values)
    {
        foreach ((string name, object? value) in values)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static string Serialize(IReadOnlyList<string> values) => JsonSerializer.Serialize(values, JsonOptions);
    private static IReadOnlyList<string> DeserializeList(string json) => Deserialize<string[]>(json);
    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)
        ?? throw new InvalidDataException($"Canonical interaction JSON could not be deserialized as {typeof(T).Name}.");
    private static T Parse<T>(string value) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out T result)
            ? result
            : throw new InvalidDataException($"Unknown canonical interaction {typeof(T).Name} value `{value}`.");
    private static string Format(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);
    private static object? Format(DateTimeOffset? value) => value is null ? null : Format(value.Value);
    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
