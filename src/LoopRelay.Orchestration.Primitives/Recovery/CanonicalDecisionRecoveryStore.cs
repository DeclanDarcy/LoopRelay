using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Recovery;

/// <summary>
/// Production decision-continuity adapter. Session scope, lineage, and turn state remain in their
/// canonical decision tables; recovery attempts/plans use the v11 recovery case and action ledger.
/// The v9 session_recovery_* tables are migration inputs only.
/// </summary>
public sealed class CanonicalDecisionRecoveryStore(
    Repository _repository,
    SqliteRecoveryStore _continuity) : IRecoveryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<DecisionContinuityStatusSnapshot> ReadStatusAsync(CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        var scopes = new List<string>();
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "SELECT scope_id FROM decision_session_active ORDER BY activated_at;";
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) scopes.Add(reader.GetString(0));
        }
        if (scopes.Count != 1)
            return new DecisionContinuityStatusSnapshot(scopes.Count, null, null, [], null, null, null,
                scopes.Count == 0 ? null : "Multiple active decision scopes exist.");
        ActiveStateReadResult active = await _continuity.ReadActiveAsync(scopes[0], cancellationToken);
        RecoveryAttempt? latest = await ReadLatestAttemptAsync(scopes[0], cancellationToken);
        RecoveryAttempt? unresolved = latest is not null && !Terminal(latest.Status) ? latest : null;
        return new DecisionContinuityStatusSnapshot(
            1, active.Active, active.Lineage, active.Lineage is null ? [] : [active.Lineage],
            latest, unresolved, null, active.Diagnostic);
    }

    public Task<ActiveStateReadResult> ReadActiveAsync(string scopeId, CancellationToken cancellationToken = default) =>
        _continuity.ReadActiveAsync(scopeId, cancellationToken);
    public Task<RecoveryStoreWriteResult> CreateScopeAndActivateAsync(DecisionSessionScopeRecord scope, DecisionSessionLineageNode lineage, DecisionSessionActiveState active, SessionContinuityProfile profile, CancellationToken cancellationToken = default) =>
        _continuity.CreateScopeAndActivateAsync(scope, lineage, active, profile, cancellationToken);
    public Task<RecoveryStoreWriteResult> UpdateActiveAccountingAsync(DecisionSessionActiveState expected, DecisionSessionActiveState updated, CancellationToken cancellationToken = default) =>
        _continuity.UpdateActiveAccountingAsync(expected, updated, cancellationToken);
    public Task<RecoveryStoreWriteResult> RecordPlannedSuccessorAsync(DecisionSessionActiveState expectedActive, DecisionSessionLineageNode successor, CancellationToken cancellationToken = default) =>
        _continuity.RecordPlannedSuccessorAsync(expectedActive, successor, cancellationToken);
    public Task<DecisionSessionLineageNode?> ReadLineageAsync(string lineageId, CancellationToken cancellationToken = default) =>
        _continuity.ReadLineageAsync(lineageId, cancellationToken);
    public Task<RecoveryStoreWriteResult> RetireScopeAsync(string scopeId, long expectedActiveRowVersion, CancellationToken cancellationToken = default) =>
        _continuity.RetireScopeAsync(scopeId, expectedActiveRowVersion, cancellationToken);
    public Task<DecisionSessionTurnRecord?> ReadDecisionTurnAsync(string transitionRunId, string inputSnapshotHash, CancellationToken cancellationToken = default) =>
        _continuity.ReadDecisionTurnAsync(transitionRunId, inputSnapshotHash, cancellationToken);
    public Task<RecoveryStoreWriteResult> BeginDecisionTurnAsync(DecisionSessionTurnRecord turn, CancellationToken cancellationToken = default) =>
        _continuity.BeginDecisionTurnAsync(turn, cancellationToken);
    public Task<RecoveryStoreWriteResult> CompareAndSwapDecisionTurnAsync(DecisionSessionTurnRecord expected, DecisionSessionTurnRecord updated, CancellationToken cancellationToken = default) =>
        _continuity.CompareAndSwapDecisionTurnAsync(expected, updated, cancellationToken);
    public Task<DecisionTurnCommitResult> CommitDecisionOutputAsync(DecisionSessionTurnRecord expectedTurn, DecisionSessionActiveState expectedActive, DecisionSessionAccounting accounting, string output, string policyDigest, CancellationToken cancellationToken = default) =>
        _continuity.CommitDecisionOutputAsync(expectedTurn, expectedActive, accounting, output, policyDigest, cancellationToken);
    public Task<RecoveryStoreWriteResult> MarkDecisionArtifactMaterializedAsync(DecisionSessionTurnRecord expected, CancellationToken cancellationToken = default) =>
        _continuity.MarkDecisionArtifactMaterializedAsync(expected, cancellationToken);

    public async Task<RecoveryStoreWriteResult> BeginAttemptAsync(
        RecoveryAttempt attempt,
        long expectedActiveRowVersion,
        SessionContinuityProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (attempt.Status != RecoveryAttemptStatus.Pending || attempt.RowVersion != 0 ||
            !string.Equals(attempt.ProfileDigest, profile.Digest, StringComparison.Ordinal))
            throw new InvalidOperationException("A canonical recovery attempt must begin Pending at row version zero.");
        ActiveStateReadResult active = await _continuity.ReadActiveAsync(attempt.ScopeId, cancellationToken);
        if (active.Status != ActiveStateReadStatus.Present || active.Active?.RowVersion != expectedActiveRowVersion ||
            active.Lineage?.LineageId != attempt.OriginalLineageId)
            return Conflict("The active decision lineage changed before recovery began.");
        if (await ReadNonterminalAttemptAsync(attempt.ScopeId, cancellationToken) is not null)
            return Conflict("A nonterminal canonical recovery attempt already exists for this scope.");
        await AppendAttemptEventAsync(attempt, cancellationToken);
        return Success(attempt.RowVersion);
    }

    public async Task<RecoveryStoreWriteResult> CompareAndSwapAttemptAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        CancellationToken cancellationToken = default)
    {
        RecoveryAttempt? current = await ReadAttemptAsync(expected.AttemptId, cancellationToken);
        if (current is null || current.RowVersion != expected.RowVersion || current.Status != expected.Status ||
            updated.RowVersion != expected.RowVersion + 1)
            return Conflict("The canonical recovery attempt changed before compare-and-swap.");
        await AppendAttemptEventAsync(updated, cancellationToken);
        return Success(updated.RowVersion);
    }

    public async Task<RecoveryStoreWriteResult> RecordPlanAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        RecoveryPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (plan.Digest != RecoveryPlanSerializer.ComputeDigest(plan) || updated.PlanDigest != plan.Digest)
            throw new InvalidOperationException("The rich recovery plan digest does not match its journal transition.");
        string caseId = await CaseIdAsync(expected.ScopeId, cancellationToken)
            ?? throw new InvalidOperationException("Canonical warm-session recovery case was not persisted before planning.");
        string classificationId = await LatestClassificationIdAsync(caseId, cancellationToken)
            ?? throw new InvalidOperationException("Canonical warm-session recovery classification is missing.");
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO canonical_recovery_plans (
                plan_id, case_id, classification_id, action, resolved_policy_identity,
                exact_profile_identity, source_evidence_json, preconditions_json,
                postconditions_json, idempotency_key, new_attempt_id,
                compatibility_document_json, planned_at
            ) VALUES ($plan, $case, $classification, $action, $policy, $profile,
                $evidence, '[]', '[]', $idempotency, NULL, $document, $planned)
            ON CONFLICT(idempotency_key) DO NOTHING;
            """;
        Add(command,
            ("$plan", plan.PlanId), ("$case", caseId), ("$classification", classificationId),
            ("$action", Action(plan.Mechanism).ToString()), ("$policy", plan.PolicyVersion),
            ("$profile", plan.ContinuityProfileDigest),
            ("$evidence", JsonSerializer.Serialize(plan.Sources.Select(source => source.Digest), JsonOptions)),
            ("$idempotency", $"rich-session-plan:{plan.Digest}"),
            ("$document", RecoveryPlanSerializer.Serialize(plan)), ("$planned", Format(updated.UpdatedAt)));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await CompareAndSwapAttemptAsync(expected, updated, cancellationToken);
    }

    public async Task<RecoveryStoreWriteResult> RecordReplacementAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        DecisionSessionLineageNode replacement,
        CancellationToken cancellationToken = default)
    {
        if (updated.ReplacementLineageId != replacement.LineageId || replacement.AuthorityState != "Inactive")
            throw new InvalidOperationException("Replacement lineage does not match the canonical recovery attempt.");
        RecoveryStoreWriteResult lineage = await _continuity.InsertInactiveRecoveryLineageAsync(replacement, cancellationToken);
        if (!lineage.Succeeded) return lineage;
        return await CompareAndSwapAttemptAsync(expected, updated, cancellationToken);
    }

    public async Task<RecoveryStoreWriteResult> CompleteRecoveryAndActivateAsync(
        RecoveryAttempt expected,
        RecoveryAttempt completed,
        DecisionSessionActiveState expectedActive,
        DecisionSessionLineageNode replacement,
        CancellationToken cancellationToken = default)
    {
        if (completed.Status != RecoveryAttemptStatus.RecoveryCompleted)
            throw new InvalidOperationException("Only a completed canonical recovery attempt may activate lineage.");
        RecoveryStoreWriteResult activation = await _continuity.ActivateRecoveryLineageAsync(
            expectedActive, replacement, completed.UpdatedAt, cancellationToken);
        if (!activation.Succeeded) return activation;
        return await CompareAndSwapAttemptAsync(expected, completed, cancellationToken);
    }

    public async Task<RecoveryAttempt?> ReadAttemptAsync(string attemptId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT document_json FROM canonical_recovery_action_events
            WHERE action_id = $attempt AND document_json IS NOT NULL
            ORDER BY event_id DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$attempt", attemptId);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? JsonSerializer.Deserialize<RecoveryAttempt>(json, JsonOptions) : null;
    }

    public async Task<RecoveryPlan?> ReadPlanAsync(string planDigest, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT compatibility_document_json FROM canonical_recovery_plans
            WHERE idempotency_key = $idempotency LIMIT 1;
            """;
        command.Parameters.AddWithValue("$idempotency", $"rich-session-plan:{planDigest}");
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? RecoveryPlanSerializer.Deserialize(json) : null;
    }

    public async Task<RecoveryAttempt?> ReadNonterminalAttemptAsync(string scopeId, CancellationToken cancellationToken = default)
    {
        RecoveryAttempt? latest = await ReadLatestAttemptAsync(scopeId, cancellationToken);
        return latest is not null && !Terminal(latest.Status) ? latest : null;
    }

    public async Task<RecoveryAttempt?> ReadLatestAttemptAsync(string scopeId, CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT event.document_json
            FROM canonical_recovery_cases AS recovery_case
            JOIN canonical_recovery_action_events AS event
              ON json_extract(event.document_json, '$.scopeId') = recovery_case.scope_identity
            WHERE recovery_case.scope_kind = 'WarmSession'
              AND recovery_case.scope_identity = $scope
              AND event.document_json IS NOT NULL
            ORDER BY event.event_id DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$scope", scopeId);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? JsonSerializer.Deserialize<RecoveryAttempt>(json, JsonOptions) : null;
    }

    private async Task AppendAttemptEventAsync(RecoveryAttempt attempt, CancellationToken cancellationToken)
    {
        string caseId = await CaseIdAsync(attempt.ScopeId, cancellationToken)
            ?? throw new InvalidOperationException("Canonical warm-session recovery case was not persisted before action journaling.");
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO canonical_recovery_action_events (
                action_id, plan_id, lifecycle, explanation, evidence_json, document_json, recorded_at
            ) VALUES ($action, $plan, $lifecycle, $explanation, '[]', $document, $recorded);
            """;
        Add(command,
            ("$action", attempt.AttemptId), ("$plan", attempt.PlanDigest ?? $"attempt:{attempt.AttemptId}"),
            ("$lifecycle", Lifecycle(attempt.Status).ToString()),
            ("$explanation", $"Rich session recovery state: {attempt.Status}."),
            ("$document", JsonSerializer.Serialize(attempt, JsonOptions)), ("$recorded", Format(attempt.UpdatedAt)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<string?> CaseIdAsync(string scopeId, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT case_id FROM canonical_recovery_cases
            WHERE scope_kind = 'WarmSession' AND scope_identity = $scope
            ORDER BY rowid DESC LIMIT 1;
            """;
        command.Parameters.AddWithValue("$scope", scopeId);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private async Task<string?> LatestClassificationIdAsync(string caseId, CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT classification_id FROM canonical_recovery_classifications WHERE case_id = $case ORDER BY rowid DESC LIMIT 1;";
        command.Parameters.AddWithValue("$case", caseId);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        string path = LoopRelayWorkspaceDatabase.Resolve(_repository);
        SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(path);
        await connection.OpenAsync(cancellationToken);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection, cancellationToken);
        return connection;
    }

    private static CanonicalRecoveryAction Action(RecoveryMechanismKey mechanism) => mechanism.Identity switch
    {
        "NativeFork" or "native-fork" => CanonicalRecoveryAction.NativeFork,
        "NativeResume" or "native-resume" => CanonicalRecoveryAction.ResumeSession,
        _ => CanonicalRecoveryAction.ReconstructContext,
    };
    private static RecoveryActionLifecycle Lifecycle(RecoveryAttemptStatus status) => status switch
    {
        RecoveryAttemptStatus.Pending => RecoveryActionLifecycle.Planned,
        RecoveryAttemptStatus.UnknownOutcome => RecoveryActionLifecycle.Unknown,
        RecoveryAttemptStatus.ProtocolRepairRequired or RecoveryAttemptStatus.RecoveryFailed => RecoveryActionLifecycle.Failed,
        RecoveryAttemptStatus.ResumeSucceeded or RecoveryAttemptStatus.RecoveryCompleted => RecoveryActionLifecycle.Succeeded,
        _ => RecoveryActionLifecycle.Started,
    };
    private static bool Terminal(RecoveryAttemptStatus status) => status is
        RecoveryAttemptStatus.ProtocolRepairRequired or RecoveryAttemptStatus.ResumeSucceeded or
        RecoveryAttemptStatus.RecoveryCompleted or RecoveryAttemptStatus.RecoveryFailed;
    private static RecoveryStoreWriteResult Success(long version) => new(true, false, version, null);
    private static RecoveryStoreWriteResult Conflict(string diagnostic) => new(false, true, null, diagnostic);
    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static void Add(SqliteCommand command, params (string Name, object? Value)[] values)
    {
        foreach ((string name, object? value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    }
}
