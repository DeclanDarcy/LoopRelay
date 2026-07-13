using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Runtime;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Recovery;

public sealed class CanonicalRecoveryStore(Repository _repository) : ICanonicalRecoveryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<CanonicalRecoveryCase?> ReadCaseAsync(
        RecoveryCaseIdentity identity,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT scope_kind, subject_json, transition_run_id, attempt_id, created_at
            FROM canonical_recovery_cases WHERE case_id = $case;
            """;
        command.Parameters.AddWithValue("$case", identity.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        RecoveryScopeKind scope = Parse<RecoveryScopeKind>(reader.GetString(0), "recovery scope");
        string subjectJson = reader.GetString(1);
        string? transitionRunId = reader.IsDBNull(2) ? null : reader.GetString(2);
        string? attemptId = reader.IsDBNull(3) ? null : reader.GetString(3);
        DateTimeOffset createdAt = ParseDate(reader.GetString(4));
        await reader.DisposeAsync();
        RecoveryCausalSubject subject = await ReadSubjectAsync(
            connection,
            subjectJson,
            transitionRunId,
            attemptId,
            cancellationToken);
        return new CanonicalRecoveryCase(identity, scope, subject, createdAt);
    }

    public async Task<CanonicalRecoveryClassification?> ReadLatestClassificationAsync(
        RecoveryCaseIdentity identity,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT classification_id, classification, cancellation_boundary, source_evidence_json,
                   supersedes_classification_id, observed_at
            FROM canonical_recovery_classifications
            WHERE case_id = $case ORDER BY rowid DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$case", identity.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadClassification(reader, identity)
            : null;
    }

    public async Task<CanonicalRecoveryPlan?> ReadPlanAsync(
        RecoveryPlanIdentity identity,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        return await ReadPlanCoreAsync(connection, "plan_id = $value", identity.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<CanonicalRecoveryPlan>> ReadPlansAsync(
        RecoveryCaseIdentity recoveryCase,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT plan_id FROM canonical_recovery_plans WHERE case_id = $case ORDER BY rowid;";
        command.Parameters.AddWithValue("$case", recoveryCase.Value);
        var identities = new List<RecoveryPlanIdentity>();
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
                identities.Add(new RecoveryPlanIdentity(reader.GetString(0)));
        }
        var plans = new List<CanonicalRecoveryPlan>(identities.Count);
        foreach (RecoveryPlanIdentity identity in identities)
        {
            CanonicalRecoveryPlan? plan = await ReadPlanCoreAsync(
                connection, "plan_id = $value", identity.Value, cancellationToken);
            if (plan is not null) plans.Add(plan);
        }
        return plans;
    }

    public async Task<CanonicalRecoveryPlan?> ReadPlanByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        return await ReadPlanCoreAsync(connection, "idempotency_key = $value", idempotencyKey, cancellationToken);
    }

    public async Task<IReadOnlyList<CanonicalRecoveryActionEvent>> ReadActionEventsAsync(
        RecoveryPlanIdentity plan,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT action_id, lifecycle, explanation, evidence_json, recorded_at
            FROM canonical_recovery_action_events
            WHERE plan_id = $plan ORDER BY event_id;
            """;
        command.Parameters.AddWithValue("$plan", plan.Value);
        var result = new List<CanonicalRecoveryActionEvent>();
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CanonicalRecoveryActionEvent(
                new RecoveryActionIdentity(reader.GetString(0)),
                plan,
                Parse<RecoveryActionLifecycle>(reader.GetString(1), "recovery action lifecycle"),
                reader.GetString(2),
                DeserializeList(reader.GetString(3)),
                ParseDate(reader.GetString(4))));
        }
        return result;
    }

    public async Task AppendCaseAndClassificationAsync(
        CanonicalRecoveryCase recoveryCase,
        CanonicalRecoveryClassification classification,
        CancellationToken cancellationToken)
    {
        if (classification.Case != recoveryCase.Identity)
            throw new InvalidOperationException("Recovery classification does not belong to the case.");
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO canonical_recovery_cases (
                    case_id, scope_kind, scope_identity, subject_json, transition_run_id, attempt_id, created_at
                ) VALUES ($case, $scope, $scope_identity, $subject, $transition, $attempt, $created);
                """;
            Add(command,
                ("$case", recoveryCase.Identity.Value),
                ("$scope", recoveryCase.Scope.ToString()),
                ("$scope_identity", recoveryCase.Subject.SessionIdentity),
                ("$subject", JsonSerializer.Serialize(recoveryCase.Subject, JsonOptions)),
                ("$transition", recoveryCase.Subject.Causality.TransitionRun.Value),
                ("$attempt", recoveryCase.Subject.Causality.Attempt.Value),
                ("$created", Format(recoveryCase.CreatedAt)));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await AppendClassificationCoreAsync(connection, transaction, classification, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AppendClassificationAsync(
        CanonicalRecoveryClassification classification,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        string? latest = await ScalarAsync(
            connection,
            transaction,
            "SELECT classification_id FROM canonical_recovery_classifications WHERE case_id = $case ORDER BY rowid DESC LIMIT 1;",
            cancellationToken,
            ("$case", classification.Case.Value));
        if (latest is not null && classification.Supersedes?.Value != latest)
            throw new InvalidOperationException("Recovery reclassification must supersede the latest immutable classification.");
        await AppendClassificationCoreAsync(connection, transaction, classification, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task AppendPlanAsync(CanonicalRecoveryPlan plan, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO canonical_recovery_plans (
                plan_id, case_id, classification_id, action, resolved_policy_identity,
                exact_profile_identity, source_evidence_json, preconditions_json,
                postconditions_json, idempotency_key, new_attempt_id, planned_at
            ) VALUES (
                $plan, $case, $classification, $action, $policy, $profile, $evidence,
                $preconditions, $postconditions, $idempotency, $attempt, $planned
            ) ON CONFLICT(idempotency_key) DO NOTHING;
            """;
        Add(command,
            ("$plan", plan.Identity.Value), ("$case", plan.Case.Value),
            ("$classification", plan.Classification.Value), ("$action", plan.Action.ToString()),
            ("$policy", plan.ResolvedPolicyIdentity), ("$profile", plan.ExactProfileIdentity),
            ("$evidence", Serialize(plan.SourceEvidence)), ("$preconditions", Serialize(plan.Preconditions)),
            ("$postconditions", Serialize(plan.Postconditions)), ("$idempotency", plan.IdempotencyKey),
            ("$attempt", plan.NewAttempt?.Value), ("$planned", Format(plan.PlannedAt)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AppendActionEventAsync(
        CanonicalRecoveryActionEvent actionEvent,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO canonical_recovery_action_events (
                action_id, plan_id, lifecycle, explanation, evidence_json, recorded_at
            ) VALUES ($action, $plan, $lifecycle, $explanation, $evidence, $recorded);
            """;
        Add(command,
            ("$action", actionEvent.Identity.Value), ("$plan", actionEvent.Plan.Value),
            ("$lifecycle", actionEvent.Lifecycle.ToString()), ("$explanation", actionEvent.Explanation),
            ("$evidence", Serialize(actionEvent.Evidence)), ("$recorded", Format(actionEvent.RecordedAt)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AppendClassificationCoreAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CanonicalRecoveryClassification classification,
        CancellationToken cancellationToken)
    {
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO canonical_recovery_classifications (
                    classification_id, case_id, classification, cancellation_boundary,
                    source_evidence_json, supersedes_classification_id, observed_at
                ) VALUES ($classification, $case, $kind, $cancellation, $evidence, $supersedes, $observed);
                """;
            Add(command,
                ("$classification", classification.Identity.Value), ("$case", classification.Case.Value),
                ("$kind", classification.Classification.ToString()),
                ("$cancellation", classification.CancellationBoundary.ToString()),
                ("$evidence", Serialize(classification.SourceEvidence)),
                ("$supersedes", classification.Supersedes?.Value),
                ("$observed", Format(classification.ObservedAt)));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        foreach (string evidence in classification.SourceEvidence.Distinct(StringComparer.Ordinal))
        {
            await using SqliteCommand link = connection.CreateCommand();
            link.Transaction = transaction;
            link.CommandText = """
                INSERT OR IGNORE INTO canonical_recovery_source_links (
                    classification_id, source_kind, source_identity, source_digest
                ) VALUES ($classification, 'durable-evidence', $identity, $digest);
                """;
            Add(link,
                ("$classification", classification.Identity.Value),
                ("$identity", evidence),
                ("$digest", Hash(evidence)));
            await link.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static CanonicalRecoveryClassification ReadClassification(
        SqliteDataReader reader,
        RecoveryCaseIdentity recoveryCase) => new(
            new RecoveryClassificationIdentity(reader.GetString(0)),
            recoveryCase,
            Parse<RecoveryBoundaryClassification>(reader.GetString(1), "recovery classification"),
            Parse<RecoveryCancellationBoundary>(reader.GetString(2), "cancellation boundary"),
            DeserializeList(reader.GetString(3)),
            reader.IsDBNull(4) ? null : new RecoveryClassificationIdentity(reader.GetString(4)),
            ParseDate(reader.GetString(5)));

    private static async Task<CanonicalRecoveryPlan?> ReadPlanCoreAsync(
        SqliteConnection connection,
        string predicate,
        string value,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT plan_id, case_id, classification_id, action, resolved_policy_identity,
                   exact_profile_identity, source_evidence_json, preconditions_json,
                   postconditions_json, idempotency_key, new_attempt_id, planned_at
            FROM canonical_recovery_plans WHERE {predicate} LIMIT 1;
            """;
        command.Parameters.AddWithValue("$value", value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new CanonicalRecoveryPlan(
            new RecoveryPlanIdentity(reader.GetString(0)), new RecoveryCaseIdentity(reader.GetString(1)),
            new RecoveryClassificationIdentity(reader.GetString(2)),
            Parse<CanonicalRecoveryAction>(reader.GetString(3), "recovery action"),
            reader.GetString(4), reader.GetString(5), DeserializeList(reader.GetString(6)),
            DeserializeList(reader.GetString(7)), DeserializeList(reader.GetString(8)), reader.GetString(9),
            reader.IsDBNull(10) ? null : new AttemptIdentity(reader.GetString(10)), ParseDate(reader.GetString(11)));
    }

    private static async Task<RecoveryCausalSubject> ReadSubjectAsync(
        SqliteConnection connection,
        string json,
        string? transitionRunId,
        string? attemptId,
        CancellationToken cancellationToken)
    {
        try
        {
            RecoveryCausalSubject? subject = JsonSerializer.Deserialize<RecoveryCausalSubject>(json, JsonOptions);
            if (subject is not null && !subject.Causality.Workspace.IsEmpty) return subject;
        }
        catch (JsonException)
        {
            // Legacy v9 rows use a smaller descriptor and are reconstructed from the causal ledger below.
        }

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT (SELECT workspace_id FROM workspace_identity WHERE id = 1),
                   attempts.run_id, attempts.workflow_instance_id, attempts.transition_run_id, attempts.attempt_id
            FROM attempts
            WHERE ($attempt IS NOT NULL AND attempts.attempt_id = $attempt)
               OR ($attempt IS NULL AND attempts.transition_run_id = $transition)
            ORDER BY attempts.rowid DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$attempt", (object?)attemptId ?? DBNull.Value);
        command.Parameters.AddWithValue("$transition", (object?)transitionRunId ?? DBNull.Value);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Legacy recovery subject has no reconstructable causal attempt.");
        return new RecoveryCausalSubject(new CanonicalCausalContext(
            new WorkspaceIdentity(reader.GetString(0)), new RunIdentity(reader.GetString(1)),
            new WorkflowInstanceIdentity(reader.GetString(2)), new TransitionRunIdentity(reader.GetString(3)),
            new AttemptIdentity(reader.GetString(4))));
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

    private static async Task<string?> ScalarAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        Add(command, parameters);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static void Add(SqliteCommand command, params (string Name, object? Value)[] values)
    {
        foreach ((string name, object? value) in values)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }

    private static T Parse<T>(string value, string label) where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: false, out T parsed)
            ? parsed
            : throw new InvalidDataException($"Unknown {label} `{value}`.");
    private static string Serialize(IReadOnlyList<string> values) => JsonSerializer.Serialize(values, JsonOptions);
    private static IReadOnlyList<string> DeserializeList(string json) =>
        JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
    private static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
