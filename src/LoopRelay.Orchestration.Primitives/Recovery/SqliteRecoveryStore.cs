using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Recovery;

public sealed class SqliteRecoveryStore(Repository _repository) : IRecoveryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<DecisionContinuityStatusSnapshot> ReadStatusAsync(
        CancellationToken cancellationToken = default)
    {
        string? scopeId;
        int activeCount;
        await using (SqliteConnection connection = await OpenAsync(cancellationToken))
        {
            await using SqliteCommand count = connection.CreateCommand();
            count.CommandText = "SELECT count(*) FROM decision_session_active;";
            activeCount = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken));
            await using SqliteCommand latest = connection.CreateCommand();
            latest.CommandText = "SELECT scope_id FROM decision_session_active ORDER BY activated_at DESC, scope_id LIMIT 1;";
            scopeId = Convert.ToString(await latest.ExecuteScalarAsync(cancellationToken));
        }

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return new DecisionContinuityStatusSnapshot(activeCount, null, null, [], null, null, null, null);
        }

        ActiveStateReadResult active = await ReadActiveAsync(scopeId, cancellationToken);
        if (active.Status != ActiveStateReadStatus.Present || active.Active is null || active.Lineage is null)
        {
            return new DecisionContinuityStatusSnapshot(
                activeCount, active.Active, active.Lineage, [], null, null, null,
                active.Diagnostic ?? $"Active state is {active.Status}.");
        }

        var ancestry = new List<DecisionSessionLineageNode>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        DecisionSessionLineageNode? cursor = active.Lineage;
        while (cursor is not null && seen.Add(cursor.LineageId))
        {
            ancestry.Add(cursor);
            cursor = cursor.ParentLineageId is { } parent
                ? await ReadLineageAsync(parent, cancellationToken)
                : null;
        }
        if (cursor is not null)
        {
            return new DecisionContinuityStatusSnapshot(
                activeCount, active.Active, active.Lineage, ancestry, null, null, null,
                "Lineage ancestry contains a cycle.");
        }

        RecoveryAttempt? latestAttempt = await ReadLatestAttemptAsync(scopeId, cancellationToken);
        RecoveryAttempt? unresolvedAttempt = await ReadNonterminalAttemptAsync(scopeId, cancellationToken);
        DecisionSessionTurnRecord? unresolvedTurn;
        await using (SqliteConnection connection = await OpenAsync(cancellationToken))
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = TurnSelect +
                " WHERE scope_id = $scope AND state NOT IN ('Committed','Materialized') ORDER BY updated_at DESC LIMIT 1;";
            command.Parameters.AddWithValue("$scope", scopeId);
            await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            unresolvedTurn = await reader.ReadAsync(cancellationToken) ? ReadTurn(reader) : null;
        }

        return new DecisionContinuityStatusSnapshot(
            activeCount, active.Active, active.Lineage, ancestry,
            latestAttempt, unresolvedAttempt, unresolvedTurn, null);
    }

    public async Task<ActiveStateReadResult> ReadActiveAsync(
        string scopeId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                a.scope_id, a.lineage_id, a.occupancy_tokens, a.reuse_cost, a.reuse_cycles,
                a.last_cycle_cost, a.previous_cycle_cost, a.transfer_cost, a.transfer_count,
                a.previous_context_size, a.context_growth_streak, a.policy_digest,
                a.projection_digest, a.row_version, a.activated_at,
                l.scope_id, l.provider, l.provider_session_id, l.parent_lineage_id,
                l.root_lineage_id, l.mechanism, l.completeness, l.source_digest,
                l.profile_digest, l.plan_digest, l.created_at, l.activated_at,
                l.retired_at, l.authority_state
            FROM decision_session_active a
            JOIN decision_session_lineage l ON l.lineage_id = a.lineage_id
            WHERE a.scope_id = $scope_id;
            """;
        command.Parameters.AddWithValue("$scope_id", scopeId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new ActiveStateReadResult(ActiveStateReadStatus.Absent, null, null, null);
        }

        try
        {
            DecisionSessionActiveState active = ReadActive(reader);
            DecisionSessionLineageNode lineage = ReadLineage(reader, 15, active.LineageId);
            if (lineage.ScopeId != active.ScopeId || lineage.AuthorityState != "Authoritative")
            {
                return new ActiveStateReadResult(
                    ActiveStateReadStatus.Conflict, active, lineage,
                    "The active pointer and lineage authority do not agree.");
            }

            if (await reader.ReadAsync(cancellationToken))
            {
                return new ActiveStateReadResult(
                    ActiveStateReadStatus.Conflict, active, lineage,
                    "Multiple active rows were returned for one scope.");
            }

            return new ActiveStateReadResult(ActiveStateReadStatus.Present, active, lineage, null);
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or JsonException)
        {
            return new ActiveStateReadResult(ActiveStateReadStatus.Corrupt, null, null, exception.Message);
        }
    }

    public async Task<RecoveryStoreWriteResult> CreateScopeAndActivateAsync(
        DecisionSessionScopeRecord scope,
        DecisionSessionLineageNode lineage,
        DecisionSessionActiveState active,
        SessionContinuityProfile profile,
        CancellationToken cancellationToken = default)
    {
        ValidateInitialActivation(scope, lineage, active, profile);
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await InsertProfileAsync(connection, transaction, profile, cancellationToken);
            await ExecuteAsync(connection, transaction,
                """
                INSERT INTO decision_session_scopes (
                    scope_id, workspace_id, workflow_identity, prepared_epic_causal_id,
                    executable_plan_causal_id, session_role, contract_version, lifecycle_state,
                    created_at, retired_at
                ) VALUES (
                    $scope_id, $workspace_id, 'Execute', $epic, $plan, $role, $contract,
                    $lifecycle, $created_at, $retired_at
                );
                """,
                cancellationToken,
                ("$scope_id", scope.ScopeId), ("$workspace_id", scope.WorkspaceId),
                ("$epic", scope.PreparedEpicCausalId), ("$plan", scope.ExecutablePlanCausalId),
                ("$role", scope.Role), ("$contract", scope.ContractVersion),
                ("$lifecycle", scope.LifecycleState), ("$created_at", Format(scope.CreatedAt)),
                ("$retired_at", scope.RetiredAt is null ? null : Format(scope.RetiredAt.Value)));
            await InsertLineageAsync(connection, transaction, lineage, cancellationToken);
            await InsertActiveAsync(connection, transaction, active, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(active.RowVersion);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<RecoveryStoreWriteResult> BeginAttemptAsync(
        RecoveryAttempt attempt,
        long expectedActiveRowVersion,
        SessionContinuityProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (attempt.Status != RecoveryAttemptStatus.Pending || attempt.RowVersion != 0 || attempt.ProfileDigest != profile.Digest)
        {
            throw new InvalidOperationException("Begin attempt requires a version-zero Pending record and its exact profile.");
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            long count = await ScalarLongAsync(connection, transaction,
                """
                SELECT count(*) FROM decision_session_active
                WHERE scope_id = $scope_id AND lineage_id = $lineage_id AND row_version = $row_version;
                """, cancellationToken,
                ("$scope_id", attempt.ScopeId), ("$lineage_id", attempt.OriginalLineageId),
                ("$row_version", expectedActiveRowVersion));
            if (count != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict("The active session changed before the recovery attempt began.");
            }

            await InsertProfileAsync(connection, transaction, profile, cancellationToken);
            await InsertAttemptAsync(connection, transaction, attempt, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(attempt.RowVersion);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<RecoveryStoreWriteResult> UpdateActiveAccountingAsync(
        DecisionSessionActiveState expected,
        DecisionSessionActiveState updated,
        CancellationToken cancellationToken = default)
    {
        if (updated.ScopeId != expected.ScopeId || updated.LineageId != expected.LineageId
            || updated.RowVersion != expected.RowVersion + 1)
        {
            throw new InvalidOperationException("Active accounting compare-and-swap identities or version are invalid.");
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            int changed = await ExecuteAsync(connection, transaction,
                """
                UPDATE decision_session_active SET
                    occupancy_tokens = $occupancy, reuse_cost = $reuse_cost, reuse_cycles = $reuse_cycles,
                    last_cycle_cost = $last_cost, previous_cycle_cost = $previous_cost,
                    transfer_cost = $transfer_cost, transfer_count = $transfer_count,
                    previous_context_size = $context_size, context_growth_streak = $growth,
                    policy_digest = $policy, projection_digest = $projection, row_version = $next_version
                WHERE scope_id = $scope AND lineage_id = $lineage AND row_version = $expected_version;
                """, cancellationToken,
                ("$occupancy", updated.Accounting.OccupancyTokens), ("$reuse_cost", updated.Accounting.ReuseCost),
                ("$reuse_cycles", updated.Accounting.ReuseCycles), ("$last_cost", updated.Accounting.LastCycleCost),
                ("$previous_cost", updated.Accounting.PreviousCycleCost), ("$transfer_cost", updated.Accounting.TransferCost),
                ("$transfer_count", updated.Accounting.TransferCount), ("$context_size", updated.Accounting.PreviousContextSize),
                ("$growth", updated.Accounting.ContextGrowthStreak), ("$policy", updated.PolicyDigest),
                ("$projection", updated.ProjectionDigest), ("$next_version", updated.RowVersion),
                ("$scope", expected.ScopeId), ("$lineage", expected.LineageId),
                ("$expected_version", expected.RowVersion));
            if (changed != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict("The active session changed before accounting was committed.");
            }

            await transaction.CommitAsync(cancellationToken);
            return Success(updated.RowVersion);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<RecoveryStoreWriteResult> RecordPlannedSuccessorAsync(
        DecisionSessionActiveState expectedActive,
        DecisionSessionLineageNode successor,
        CancellationToken cancellationToken = default)
    {
        if (successor.ScopeId != expectedActive.ScopeId
            || successor.ParentLineageId != expectedActive.LineageId
            || successor.Mechanism != "PlannedTransfer"
            || successor.AuthorityState != "Inactive")
        {
            throw new InvalidOperationException("The planned successor does not match the active lineage.");
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            long active = await ScalarLongAsync(connection, transaction,
                "SELECT count(*) FROM decision_session_active WHERE scope_id = $scope AND lineage_id = $lineage AND row_version = $version;",
                cancellationToken, ("$scope", expectedActive.ScopeId), ("$lineage", expectedActive.LineageId),
                ("$version", expectedActive.RowVersion));
            if (active != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict("The active pointer changed before the planned successor was recorded.");
            }

            await InsertLineageAsync(connection, transaction, successor, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(expectedActive.RowVersion);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Conflict("The planned successor identity already exists.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task<RecoveryStoreWriteResult> CompareAndSwapAttemptAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        CancellationToken cancellationToken = default) =>
        UpdateAttemptInTransactionAsync(expected, updated, null, null, cancellationToken);

    public async Task<RecoveryStoreWriteResult> RecordPlanAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        RecoveryPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (plan.Digest != RecoveryPlanSerializer.ComputeDigest(plan)
            || updated.PlanDigest != plan.Digest
            || updated.Mechanism != plan.Mechanism
            || plan.ContinuityProfileDigest != updated.ProfileDigest)
        {
            throw new InvalidOperationException("The recovery plan digest/profile/mechanism does not match the journal transition.");
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await ExecuteAsync(connection, transaction,
                """
                INSERT INTO session_recovery_plans (
                    plan_digest, plan_id, schema_version, planner_version, policy_version,
                    mechanism_identity, mechanism_version, activation_strategy, validation_strategy,
                    reconciliation_strategy, expected_completeness, profile_digest, canonical_json, created_at
                ) VALUES (
                    $digest, $id, $schema, $planner, $policy, $mechanism, $mechanism_version,
                    $activation, $validation, $reconciliation, $completeness, $profile, $json, $created
                ) ON CONFLICT(plan_digest) DO NOTHING;
                """, cancellationToken,
                ("$digest", plan.Digest), ("$id", plan.PlanId), ("$schema", plan.SchemaVersion),
                ("$planner", plan.PlannerVersion), ("$policy", plan.PolicyVersion),
                ("$mechanism", plan.Mechanism.Identity), ("$mechanism_version", plan.Mechanism.Version),
                ("$activation", plan.ActivationStrategy.ToString()), ("$validation", plan.ValidationStrategy),
                ("$reconciliation", plan.ReconciliationStrategy), ("$completeness", plan.ExpectedCompleteness.ToString()),
                ("$profile", plan.ContinuityProfileDigest), ("$json", RecoveryPlanSerializer.Serialize(plan)),
                ("$created", Format(updated.UpdatedAt)));

            foreach (RecoverySourceDescriptor source in plan.Sources)
            {
                await ExecuteAsync(connection, transaction,
                    """
                    INSERT INTO session_recovery_sources (
                        attempt_id, source_order, source_kind, source_location, source_digest,
                        verified_boundary, normalizer_version, completeness, omissions_json, descriptor_json
                    ) VALUES (
                        $attempt, $order, $kind, $location, $digest, $boundary, $normalizer,
                        $completeness, $omissions, $descriptor
                    );
                    """, cancellationToken,
                    ("$attempt", expected.AttemptId), ("$order", source.Order), ("$kind", source.Kind),
                    ("$location", source.Location), ("$digest", source.Digest),
                    ("$boundary", source.VerifiedBoundary), ("$normalizer", source.NormalizerVersion),
                    ("$completeness", source.Completeness.ToString()), ("$omissions", Json(source.Omissions)),
                    ("$descriptor", Json(source)));
            }

            RecoveryStoreWriteResult result = await UpdateAttemptAsync(connection, transaction, expected, updated, cancellationToken);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<RecoveryStoreWriteResult> RecordReplacementAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        DecisionSessionLineageNode replacement,
        CancellationToken cancellationToken = default)
    {
        if (updated.ReplacementLineageId != replacement.LineageId
            || replacement.ParentLineageId != expected.OriginalLineageId
            || replacement.AuthorityState != "Inactive")
        {
            throw new InvalidOperationException("Replacement lineage does not match the recovery attempt.");
        }

        return await UpdateAttemptInTransactionAsync(expected, updated, replacement, null, cancellationToken);
    }

    public async Task<RecoveryStoreWriteResult> InsertInactiveRecoveryLineageAsync(
        DecisionSessionLineageNode replacement,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            await InsertLineageAsync(connection, transaction, replacement, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Success(0);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Conflict("The inactive recovery lineage already exists or violates its scope contract.");
        }
    }

    public async Task<RecoveryStoreWriteResult> ActivateRecoveryLineageAsync(
        DecisionSessionActiveState expectedActive,
        DecisionSessionLineageNode replacement,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            int activeChanged = await ExecuteAsync(connection, transaction,
                """
                UPDATE decision_session_active
                SET lineage_id = $replacement, row_version = row_version + 1, activated_at = $activated
                WHERE scope_id = $scope AND lineage_id = $original AND row_version = $version;
                """, cancellationToken,
                ("$replacement", replacement.LineageId), ("$activated", Format(activatedAt)),
                ("$scope", expectedActive.ScopeId), ("$original", expectedActive.LineageId),
                ("$version", expectedActive.RowVersion));
            if (activeChanged != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict("The active pointer changed before canonical recovery activation.");
            }
            await ExecuteAsync(connection, transaction,
                """
                UPDATE decision_session_lineage
                SET authority_state = 'Superseded', retired_at = $at
                WHERE lineage_id = $original AND authority_state = 'Authoritative';
                UPDATE decision_session_lineage
                SET authority_state = 'Authoritative', activated_at = $at
                WHERE lineage_id = $replacement AND authority_state = 'Inactive';
                """, cancellationToken,
                ("$at", Format(activatedAt)), ("$original", expectedActive.LineageId),
                ("$replacement", replacement.LineageId));
            await transaction.CommitAsync(cancellationToken);
            return Success(expectedActive.RowVersion + 1);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<RecoveryStoreWriteResult> CompleteRecoveryAndActivateAsync(
        RecoveryAttempt expected,
        RecoveryAttempt completed,
        DecisionSessionActiveState expectedActive,
        DecisionSessionLineageNode replacement,
        CancellationToken cancellationToken = default)
    {
        if (completed.Status != RecoveryAttemptStatus.RecoveryCompleted
            || completed.ReplacementLineageId != replacement.LineageId
            || expectedActive.ScopeId != completed.ScopeId
            || expectedActive.LineageId != completed.OriginalLineageId
            || replacement.ScopeId != completed.ScopeId
            || replacement.PlanDigest != completed.PlanDigest)
        {
            throw new InvalidOperationException("Recovery completion, active pointer, lineage, and plan are inconsistent.");
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            long planCount = await ScalarLongAsync(connection, transaction,
                "SELECT count(*) FROM session_recovery_plans WHERE plan_digest = $digest;",
                cancellationToken, ("$digest", completed.PlanDigest));
            if (planCount != 1)
            {
                throw new InvalidOperationException("The persisted recovery plan was not found during activation.");
            }

            int activeChanged = await ExecuteAsync(connection, transaction,
                """
                UPDATE decision_session_active
                SET lineage_id = $replacement, row_version = row_version + 1, activated_at = $activated
                WHERE scope_id = $scope AND lineage_id = $original AND row_version = $version;
                """, cancellationToken,
                ("$replacement", replacement.LineageId), ("$activated", Format(completed.UpdatedAt)),
                ("$scope", expectedActive.ScopeId), ("$original", expectedActive.LineageId),
                ("$version", expectedActive.RowVersion));
            if (activeChanged != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict("The active pointer changed before recovery activation.");
            }

            await ExecuteAsync(connection, transaction,
                """
                UPDATE decision_session_lineage
                SET authority_state = 'Superseded', retired_at = $at
                WHERE lineage_id = $original AND authority_state = 'Authoritative';
                UPDATE decision_session_lineage
                SET authority_state = 'Authoritative', activated_at = $at
                WHERE lineage_id = $replacement AND authority_state = 'Inactive';
                """, cancellationToken,
                ("$at", Format(completed.UpdatedAt)), ("$original", expectedActive.LineageId),
                ("$replacement", replacement.LineageId));

            RecoveryStoreWriteResult attemptResult = await UpdateAttemptAsync(
                connection, transaction, expected, completed, cancellationToken);
            if (!attemptResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return attemptResult;
            }

            long authorityCount = await ScalarLongAsync(connection, transaction,
                """
                SELECT count(*) FROM decision_session_lineage
                WHERE scope_id = $scope AND authority_state = 'Authoritative';
                """, cancellationToken, ("$scope", completed.ScopeId));
            if (authorityCount != 1)
            {
                throw new InvalidOperationException("Atomic activation did not produce exactly one authoritative lineage node.");
            }

            await transaction.CommitAsync(cancellationToken);
            return attemptResult;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<RecoveryAttempt?> ReadAttemptAsync(
        string attemptId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = AttemptSelect + " WHERE attempt_id = $attempt_id;";
        command.Parameters.AddWithValue("$attempt_id", attemptId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAttempt(reader) : null;
    }

    public async Task<RecoveryPlan?> ReadPlanAsync(
        string planDigest,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT canonical_json FROM session_recovery_plans WHERE plan_digest = $digest;";
        command.Parameters.AddWithValue("$digest", planDigest);
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json ? RecoveryPlanSerializer.Deserialize(json) : null;
    }

    public async Task<DecisionSessionLineageNode?> ReadLineageAsync(
        string lineageId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            SELECT scope_id, provider, provider_session_id, parent_lineage_id, root_lineage_id,
                   mechanism, completeness, source_digest, profile_digest, plan_digest,
                   created_at, activated_at, retired_at, authority_state
            FROM decision_session_lineage WHERE lineage_id = $lineage;
            """;
        command.Parameters.AddWithValue("$lineage", lineageId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadLineage(reader, 0, lineageId) : null;
    }

    public async Task<RecoveryAttempt?> ReadNonterminalAttemptAsync(
        string scopeId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = AttemptSelect + """
             WHERE scope_id = $scope_id
               AND status NOT IN ('ProtocolRepairRequired','ResumeSucceeded','RecoveryCompleted','RecoveryFailed')
             ORDER BY created_at DESC LIMIT 2;
            """;
        command.Parameters.AddWithValue("$scope_id", scopeId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        RecoveryAttempt result = ReadAttempt(reader);
        if (await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Multiple nonterminal recovery attempts exist for one scope.");
        }

        return result;
    }

    public async Task<RecoveryAttempt?> ReadLatestAttemptAsync(
        string scopeId,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = AttemptSelect +
            " WHERE scope_id = $scope_id ORDER BY created_at DESC, attempt_id DESC LIMIT 1;";
        command.Parameters.AddWithValue("$scope_id", scopeId);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadAttempt(reader) : null;
    }

    public async Task<RecoveryStoreWriteResult> RetireScopeAsync(
        string scopeId,
        long expectedActiveRowVersion,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            string? lineageId = await ScalarStringAsync(connection, transaction,
                "SELECT lineage_id FROM decision_session_active WHERE scope_id = $scope AND row_version = $version;",
                cancellationToken, ("$scope", scopeId), ("$version", expectedActiveRowVersion));
            if (lineageId is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict("The active scope changed before retirement.");
            }

            string at = Format(DateTimeOffset.UtcNow);
            await ExecuteAsync(connection, transaction,
                """
                DELETE FROM decision_session_active WHERE scope_id = $scope AND row_version = $version;
                UPDATE decision_session_lineage SET authority_state = 'Retired', retired_at = $at WHERE lineage_id = $lineage;
                UPDATE decision_session_scopes SET lifecycle_state = 'Retired', retired_at = $at WHERE scope_id = $scope;
                """, cancellationToken,
                ("$scope", scopeId), ("$version", expectedActiveRowVersion), ("$at", at), ("$lineage", lineageId));
            await transaction.CommitAsync(cancellationToken);
            return Success(expectedActiveRowVersion + 1);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<DecisionSessionTurnRecord?> ReadDecisionTurnAsync(
        string transitionRunId,
        string inputSnapshotHash,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = TurnSelect + " WHERE transition_run_id = $run AND input_snapshot_hash = $hash;";
        command.Parameters.AddWithValue("$run", transitionRunId);
        command.Parameters.AddWithValue("$hash", inputSnapshotHash);
        await using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTurn(reader) : null;
    }

    public async Task<RecoveryStoreWriteResult> BeginDecisionTurnAsync(
        DecisionSessionTurnRecord turn,
        CancellationToken cancellationToken = default)
    {
        if (turn.State != DecisionTurnState.Pending || turn.RowVersion != 0
            || turn.WriteStarted || turn.Submitted || turn.Accepted || turn.Terminal)
        {
            throw new InvalidOperationException("A new decision turn must be a version-zero Pending intent.");
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            long active = await ScalarLongAsync(connection, transaction,
                """
                SELECT count(*)
                FROM decision_session_active a
                LEFT JOIN decision_session_lineage successor
                  ON successor.lineage_id = $lineage
                 AND successor.scope_id = a.scope_id
                 AND successor.parent_lineage_id = a.lineage_id
                 AND successor.mechanism = 'PlannedTransfer'
                 AND successor.authority_state = 'Inactive'
                WHERE a.scope_id = $scope
                  AND (a.lineage_id = $lineage OR successor.lineage_id IS NOT NULL);
                """,
                cancellationToken, ("$scope", turn.ScopeId), ("$lineage", turn.LineageId));
            if (active != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict("The decision turn does not target the active lineage.");
            }

            await ExecuteAsync(connection, transaction,
                """
                INSERT INTO decision_session_turns (
                    turn_record_id, scope_id, lineage_id, transition_run_id, input_snapshot_hash,
                    provider_thread_id, provider_turn_id, request_id, state, write_started,
                    submitted, accepted, terminal, output_body, output_hash, history_kind,
                    history_sequence, artifact_materialized, reconciliation_json, row_version,
                    created_at, updated_at
                ) VALUES (
                    $id, $scope, $lineage, $run, $input, $thread, $provider_turn, $request,
                    $state, 0, 0, 0, 0, NULL, NULL, NULL, NULL, 0, NULL, 0, $created, $updated
                );
                """, cancellationToken,
                ("$id", turn.TurnRecordId), ("$scope", turn.ScopeId), ("$lineage", turn.LineageId),
                ("$run", turn.TransitionRunId), ("$input", turn.InputSnapshotHash),
                ("$thread", turn.ProviderThreadId), ("$provider_turn", turn.ProviderTurnId),
                ("$request", turn.RequestId), ("$state", turn.State.ToString()),
                ("$created", Format(turn.CreatedAt)), ("$updated", Format(turn.UpdatedAt)));
            await transaction.CommitAsync(cancellationToken);
            return Success(turn.RowVersion);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Conflict("A decision turn already exists for this transition run and input hash.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<RecoveryStoreWriteResult> CompareAndSwapDecisionTurnAsync(
        DecisionSessionTurnRecord expected,
        DecisionSessionTurnRecord updated,
        CancellationToken cancellationToken = default)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            RecoveryStoreWriteResult result = await UpdateTurnAsync(connection, transaction, expected, updated, cancellationToken);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<DecisionTurnCommitResult> CommitDecisionOutputAsync(
        DecisionSessionTurnRecord expectedTurn,
        DecisionSessionActiveState expectedActive,
        DecisionSessionAccounting accounting,
        string output,
        string policyDigest,
        CancellationToken cancellationToken = default)
    {
        if (expectedTurn.State != DecisionTurnState.Terminal || !expectedTurn.Terminal
            || expectedTurn.ScopeId != expectedActive.ScopeId)
        {
            throw new InvalidOperationException("Only a terminal turn in the expected active scope can commit output.");
        }

        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            bool plannedTransfer = expectedTurn.LineageId != expectedActive.LineageId;
            if (plannedTransfer)
            {
                long validSuccessor = await ScalarLongAsync(connection, transaction,
                    """
                    SELECT count(*) FROM decision_session_lineage
                    WHERE lineage_id = $successor AND scope_id = $scope
                      AND parent_lineage_id = $original AND mechanism = 'PlannedTransfer'
                      AND authority_state = 'Inactive';
                    """, cancellationToken,
                    ("$successor", expectedTurn.LineageId), ("$scope", expectedActive.ScopeId),
                    ("$original", expectedActive.LineageId));
                if (validSuccessor != 1)
                {
                    throw new InvalidOperationException(
                        "The decision turn does not target the active lineage or a valid planned successor.");
                }
            }

            int sequence = checked((int)(await ScalarLongAsync(connection, transaction,
                "SELECT COALESCE(MAX(sequence), 0) + 1 FROM loop_history WHERE kind = 'decisions';",
                cancellationToken)));
            string relativePath = $".agents/decisions/decisions.{sequence:0000}.md";
            string outputHash = Sha256(output);
            await ExecuteAsync(connection, transaction,
                """
                INSERT INTO loop_history (
                    kind, sequence, logical_path, body, content_hash, created_at,
                    producer_run_id, producer_lineage_id, provider_thread_id, provider_turn_id,
                    recovery_attempt_id
                ) VALUES (
                    'decisions', $sequence, $path, $body, $hash, $created,
                    $run, $lineage, $thread, $turn, NULL
                );
                """, cancellationToken,
                ("$sequence", sequence), ("$path", relativePath), ("$body", output), ("$hash", outputHash),
                ("$created", Format(DateTimeOffset.UtcNow)), ("$run", expectedTurn.TransitionRunId),
                ("$lineage", expectedTurn.LineageId), ("$thread", expectedTurn.ProviderThreadId),
                ("$turn", expectedTurn.ProviderTurnId));

            DecisionSessionActiveState updatedActive = expectedActive with
            {
                LineageId = expectedTurn.LineageId,
                Accounting = accounting,
                PolicyDigest = policyDigest,
                RowVersion = expectedActive.RowVersion + 1,
            };
            int activeChanged = await ExecuteAsync(connection, transaction,
                """
                UPDATE decision_session_active SET
                    lineage_id = $next_lineage,
                    occupancy_tokens = $occupancy, reuse_cost = $reuse_cost, reuse_cycles = $reuse_cycles,
                    last_cycle_cost = $last_cost, previous_cycle_cost = $previous_cost,
                    transfer_cost = $transfer_cost, transfer_count = $transfer_count,
                    previous_context_size = $context_size, context_growth_streak = $growth,
                    policy_digest = $policy, row_version = $next_version
                WHERE scope_id = $scope AND lineage_id = $lineage AND row_version = $expected_version;
                """, cancellationToken,
                ("$occupancy", accounting.OccupancyTokens), ("$reuse_cost", accounting.ReuseCost),
                ("$reuse_cycles", accounting.ReuseCycles), ("$last_cost", accounting.LastCycleCost),
                ("$previous_cost", accounting.PreviousCycleCost), ("$transfer_cost", accounting.TransferCost),
                ("$transfer_count", accounting.TransferCount), ("$context_size", accounting.PreviousContextSize),
                ("$growth", accounting.ContextGrowthStreak), ("$policy", policyDigest),
                ("$next_version", updatedActive.RowVersion), ("$scope", expectedActive.ScopeId),
                ("$lineage", expectedActive.LineageId), ("$next_lineage", expectedTurn.LineageId),
                ("$expected_version", expectedActive.RowVersion));
            if (activeChanged != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DecisionTurnCommitResult(Conflict("Active accounting changed before decision commit."), null, null, null);
            }

            if (plannedTransfer)
            {
                await ExecuteAsync(connection, transaction,
                    """
                    UPDATE decision_session_lineage
                    SET authority_state = 'Superseded', retired_at = $at
                    WHERE lineage_id = $original AND authority_state = 'Authoritative';
                    UPDATE decision_session_lineage
                    SET authority_state = 'Authoritative', activated_at = $at
                    WHERE lineage_id = $successor AND authority_state = 'Inactive';
                    """, cancellationToken,
                    ("$at", Format(DateTimeOffset.UtcNow)), ("$original", expectedActive.LineageId),
                    ("$successor", expectedTurn.LineageId));
            }

            DecisionSessionTurnRecord committed = expectedTurn with
            {
                State = DecisionTurnState.Committed,
                OutputBody = output,
                OutputHash = outputHash,
                HistoryKind = "decisions",
                HistorySequence = sequence,
                RowVersion = expectedTurn.RowVersion + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            RecoveryStoreWriteResult turnWrite = await UpdateTurnAsync(
                connection, transaction, expectedTurn, committed, cancellationToken);
            if (!turnWrite.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new DecisionTurnCommitResult(turnWrite, null, null, null);
            }

            await ExecuteAsync(connection, transaction,
                """
                INSERT INTO session_transition_correlations (
                    transition_run_id, looprelay_session_id, lineage_id, recovery_attempt_id,
                    provider_thread_id, provider_turn_id, turn_record_id, created_at
                ) VALUES (
                    $run, $session, $lineage, NULL, $thread, $turn, $turn_record, $created
                ) ON CONFLICT(transition_run_id) DO UPDATE SET
                    lineage_id = excluded.lineage_id,
                    provider_thread_id = excluded.provider_thread_id,
                    provider_turn_id = excluded.provider_turn_id,
                    turn_record_id = excluded.turn_record_id;
                """, cancellationToken,
                ("$run", expectedTurn.TransitionRunId), ("$session", expectedTurn.ScopeId),
                ("$lineage", expectedTurn.LineageId), ("$thread", expectedTurn.ProviderThreadId),
                ("$turn", expectedTurn.ProviderTurnId), ("$turn_record", expectedTurn.TurnRecordId),
                ("$created", Format(DateTimeOffset.UtcNow)));

            await transaction.CommitAsync(cancellationToken);
            return new DecisionTurnCommitResult(turnWrite, committed, updatedActive, relativePath);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<RecoveryStoreWriteResult> MarkDecisionArtifactMaterializedAsync(
        DecisionSessionTurnRecord expected,
        CancellationToken cancellationToken = default)
    {
        if (expected.State is not DecisionTurnState.Committed and not DecisionTurnState.Materialized)
        {
            throw new InvalidOperationException("Only a committed decision output can be materialized.");
        }

        if (expected.State == DecisionTurnState.Materialized)
        {
            return Success(expected.RowVersion);
        }

        DecisionSessionTurnRecord updated = expected with
        {
            State = DecisionTurnState.Materialized,
            ArtifactMaterialized = true,
            RowVersion = expected.RowVersion + 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        return await CompareAndSwapDecisionTurnAsync(expected, updated, cancellationToken);
    }

    private async Task<RecoveryStoreWriteResult> UpdateAttemptInTransactionAsync(
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        DecisionSessionLineageNode? replacement,
        Func<SqliteConnection, SqliteTransaction, CancellationToken, Task>? extra,
        CancellationToken cancellationToken)
    {
        await using SqliteConnection connection = await OpenAsync(cancellationToken);
        await using SqliteTransaction transaction = connection.BeginTransaction(deferred: false);
        try
        {
            if (replacement is not null)
            {
                await InsertLineageAsync(connection, transaction, replacement, cancellationToken);
            }

            if (extra is not null)
            {
                await extra(connection, transaction, cancellationToken);
            }

            RecoveryStoreWriteResult result = await UpdateAttemptAsync(connection, transaction, expected, updated, cancellationToken);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return result;
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<RecoveryStoreWriteResult> UpdateAttemptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        RecoveryAttempt expected,
        RecoveryAttempt updated,
        CancellationToken cancellationToken)
    {
        if (updated.AttemptId != expected.AttemptId || updated.RowVersion != expected.RowVersion + 1)
        {
            throw new InvalidOperationException("Attempt compare-and-swap requires the same identity and next row version.");
        }

        int changed = await ExecuteAsync(connection, transaction,
            """
            UPDATE session_recovery_attempts SET
                replacement_lineage_id = $replacement, status = $status, row_version = $next_version,
                plan_digest = $plan, failure_classification = $failure_classification,
                failure_json = $failure_json, mechanism_identity = $mechanism,
                mechanism_version = $mechanism_version, provider_request_id = $request,
                provider_correlation_id = $correlation, retry_count = $retry,
                updated_at = $updated, completed_at = $completed
            WHERE attempt_id = $attempt AND row_version = $expected_version AND status = $expected_status;
            """, cancellationToken,
            ("$replacement", updated.ReplacementLineageId), ("$status", updated.Status.ToString()),
            ("$next_version", updated.RowVersion), ("$plan", updated.PlanDigest),
            ("$failure_classification", updated.Failure?.Classification),
            ("$failure_json", updated.Failure is null ? null : Json(updated.Failure)),
            ("$mechanism", updated.Mechanism?.Identity), ("$mechanism_version", updated.Mechanism?.Version),
            ("$request", updated.ProviderRequestId), ("$correlation", updated.ProviderCorrelationId),
            ("$retry", updated.RetryCount), ("$updated", Format(updated.UpdatedAt)),
            ("$completed", updated.CompletedAt is null ? null : Format(updated.CompletedAt.Value)),
            ("$attempt", expected.AttemptId), ("$expected_version", expected.RowVersion),
            ("$expected_status", expected.Status.ToString()));
        return changed == 1 ? Success(updated.RowVersion) : Conflict("Recovery attempt compare-and-swap conflict.");
    }

    private static async Task InsertAttemptAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        RecoveryAttempt attempt,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(connection, transaction,
            """
            INSERT INTO session_recovery_attempts (
                attempt_id, previous_attempt_id, scope_id, original_lineage_id, replacement_lineage_id,
                transition_run_id, status, row_version, profile_digest, plan_digest,
                failure_classification, failure_json, trigger, mechanism_identity, mechanism_version,
                idempotency_key, provider_request_id, provider_correlation_id, retry_count,
                diagnostic_json, created_at, updated_at, completed_at
            ) VALUES (
                $id, $previous, $scope, $original, $replacement, $run, $status, $version,
                $profile, $plan, $failure, $failure_json, $trigger, $mechanism, $mechanism_version,
                $idempotency, $request, $correlation, $retry, NULL, $created, $updated, $completed
            );
            """, cancellationToken,
            ("$id", attempt.AttemptId), ("$previous", attempt.PreviousAttemptId), ("$scope", attempt.ScopeId),
            ("$original", attempt.OriginalLineageId), ("$replacement", attempt.ReplacementLineageId),
            ("$run", attempt.TransitionRunId), ("$status", attempt.Status.ToString()),
            ("$version", attempt.RowVersion), ("$profile", attempt.ProfileDigest), ("$plan", attempt.PlanDigest),
            ("$failure", attempt.Failure?.Classification),
            ("$failure_json", attempt.Failure is null ? null : Json(attempt.Failure)),
            ("$trigger", attempt.Trigger), ("$mechanism", attempt.Mechanism?.Identity),
            ("$mechanism_version", attempt.Mechanism?.Version), ("$idempotency", attempt.IdempotencyKey),
            ("$request", attempt.ProviderRequestId), ("$correlation", attempt.ProviderCorrelationId),
            ("$retry", attempt.RetryCount), ("$created", Format(attempt.CreatedAt)),
            ("$updated", Format(attempt.UpdatedAt)),
            ("$completed", attempt.CompletedAt is null ? null : Format(attempt.CompletedAt.Value)));

    private static async Task InsertProfileAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        SessionContinuityProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile.Digest != SessionContinuityProfileDigest.Compute(profile))
        {
            throw new InvalidOperationException("Continuity profile digest is invalid.");
        }

        string json = Json(profile);
        await ExecuteAsync(connection, transaction,
            """
            INSERT INTO session_continuity_profiles (
                profile_digest, provider, server_version, schema_digest, profile_json, evidence_source, created_at
            ) VALUES ($digest, $provider, $server, $schema, $json, $evidence, $created)
            ON CONFLICT(profile_digest) DO NOTHING;
            """, cancellationToken,
            ("$digest", profile.Digest), ("$provider", profile.Provider), ("$server", profile.ServerVersion),
            ("$schema", profile.SchemaDigest), ("$json", json), ("$evidence", profile.EvidenceSource),
            ("$created", Format(profile.NegotiatedAt)));
        string? stored = await ScalarStringAsync(connection, transaction,
            "SELECT profile_json FROM session_continuity_profiles WHERE profile_digest = $digest;",
            cancellationToken, ("$digest", profile.Digest));
        if (!string.Equals(stored, json, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A profile digest collision or noncanonical profile record was detected.");
        }
    }

    private static Task InsertLineageAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DecisionSessionLineageNode lineage,
        CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction,
            """
            INSERT INTO decision_session_lineage (
                lineage_id, scope_id, provider, provider_session_id, parent_lineage_id, root_lineage_id,
                mechanism, completeness, source_digest, profile_digest, plan_digest,
                created_at, activated_at, retired_at, authority_state
            ) VALUES (
                $id, $scope, $provider, $session, $parent, $root, $mechanism, $completeness,
                $source, $profile, $plan, $created, $activated, $retired, $authority
            );
            """, cancellationToken,
            ("$id", lineage.LineageId), ("$scope", lineage.ScopeId), ("$provider", lineage.Provider),
            ("$session", lineage.ProviderSessionId), ("$parent", lineage.ParentLineageId),
            ("$root", lineage.RootLineageId), ("$mechanism", lineage.Mechanism),
            ("$completeness", lineage.Completeness.ToString()), ("$source", lineage.SourceDigest),
            ("$profile", lineage.ProfileDigest), ("$plan", lineage.PlanDigest),
            ("$created", Format(lineage.CreatedAt)),
            ("$activated", lineage.ActivatedAt is null ? null : Format(lineage.ActivatedAt.Value)),
            ("$retired", lineage.RetiredAt is null ? null : Format(lineage.RetiredAt.Value)),
            ("$authority", lineage.AuthorityState));

    private static Task InsertActiveAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DecisionSessionActiveState active,
        CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction,
            """
            INSERT INTO decision_session_active (
                scope_id, lineage_id, occupancy_tokens, reuse_cost, reuse_cycles, last_cycle_cost,
                previous_cycle_cost, transfer_cost, transfer_count, previous_context_size,
                context_growth_streak, policy_digest, projection_digest, row_version, activated_at
            ) VALUES (
                $scope, $lineage, $occupancy, $reuse_cost, $reuse_cycles, $last_cost,
                $previous_cost, $transfer_cost, $transfer_count, $context_size,
                $growth, $policy, $projection, $version, $activated
            );
            """, cancellationToken,
            ("$scope", active.ScopeId), ("$lineage", active.LineageId),
            ("$occupancy", active.Accounting.OccupancyTokens), ("$reuse_cost", active.Accounting.ReuseCost),
            ("$reuse_cycles", active.Accounting.ReuseCycles), ("$last_cost", active.Accounting.LastCycleCost),
            ("$previous_cost", active.Accounting.PreviousCycleCost), ("$transfer_cost", active.Accounting.TransferCost),
            ("$transfer_count", active.Accounting.TransferCount), ("$context_size", active.Accounting.PreviousContextSize),
            ("$growth", active.Accounting.ContextGrowthStreak), ("$policy", active.PolicyDigest),
            ("$projection", active.ProjectionDigest), ("$version", active.RowVersion),
            ("$activated", Format(active.ActivatedAt)));

    private static void ValidateInitialActivation(
        DecisionSessionScopeRecord scope,
        DecisionSessionLineageNode lineage,
        DecisionSessionActiveState active,
        SessionContinuityProfile profile)
    {
        if (lineage.ScopeId != scope.ScopeId || active.ScopeId != scope.ScopeId
            || active.LineageId != lineage.LineageId || lineage.ParentLineageId is not null
            || lineage.RootLineageId != lineage.LineageId || lineage.AuthorityState != "Authoritative"
            || lineage.ProfileDigest != profile.Digest)
        {
            throw new InvalidOperationException("Initial scope, lineage, active pointer, and profile are inconsistent.");
        }
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

    private const string AttemptSelect = """
        SELECT attempt_id, previous_attempt_id, scope_id, original_lineage_id, replacement_lineage_id,
               transition_run_id, status, row_version, profile_digest, plan_digest,
               failure_json, trigger, mechanism_identity, mechanism_version, idempotency_key,
               provider_request_id, provider_correlation_id, retry_count, created_at, updated_at, completed_at
        FROM session_recovery_attempts
        """;

    private const string TurnSelect = """
        SELECT turn_record_id, scope_id, lineage_id, transition_run_id, input_snapshot_hash,
               provider_thread_id, provider_turn_id, request_id, state, write_started,
               submitted, accepted, terminal, output_body, output_hash, history_kind,
               history_sequence, artifact_materialized, reconciliation_json, row_version,
               created_at, updated_at
        FROM decision_session_turns
        """;

    private static RecoveryAttempt ReadAttempt(SqliteDataReader reader) => new(
        reader.GetString(0), NullableString(reader, 1), reader.GetString(2), reader.GetString(3),
        NullableString(reader, 4), NullableString(reader, 5), Enum.Parse<RecoveryAttemptStatus>(reader.GetString(6)),
        reader.GetInt64(7), reader.GetString(8), NullableString(reader, 9),
        reader.IsDBNull(10) ? null : JsonSerializer.Deserialize<RecoveryFailure>(reader.GetString(10), JsonOptions),
        reader.GetString(11),
        reader.IsDBNull(12) ? null : new RecoveryMechanismKey(reader.GetString(12), reader.GetString(13)),
        reader.GetString(14), NullableString(reader, 15), NullableString(reader, 16), reader.GetInt32(17),
        ParseTime(reader.GetString(18)), ParseTime(reader.GetString(19)),
        reader.IsDBNull(20) ? null : ParseTime(reader.GetString(20)));

    private static DecisionSessionTurnRecord ReadTurn(SqliteDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
        reader.GetString(5), NullableString(reader, 6), NullableString(reader, 7),
        Enum.Parse<DecisionTurnState>(reader.GetString(8)), reader.GetBoolean(9), reader.GetBoolean(10),
        reader.GetBoolean(11), reader.GetBoolean(12), NullableString(reader, 13), NullableString(reader, 14),
        NullableString(reader, 15), reader.IsDBNull(16) ? null : reader.GetInt32(16), reader.GetBoolean(17),
        NullableString(reader, 18), reader.GetInt64(19), ParseTime(reader.GetString(20)), ParseTime(reader.GetString(21)));

    private static async Task<RecoveryStoreWriteResult> UpdateTurnAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DecisionSessionTurnRecord expected,
        DecisionSessionTurnRecord updated,
        CancellationToken cancellationToken)
    {
        if (updated.TurnRecordId != expected.TurnRecordId || updated.RowVersion != expected.RowVersion + 1)
        {
            throw new InvalidOperationException("Decision turn compare-and-swap identity or row version is invalid.");
        }

        int changed = await ExecuteAsync(connection, transaction,
            """
            UPDATE decision_session_turns SET
                provider_turn_id = $provider_turn, request_id = $request, state = $state,
                write_started = $write_started, submitted = $submitted, accepted = $accepted,
                terminal = $terminal, output_body = $output, output_hash = $output_hash,
                history_kind = $history_kind, history_sequence = $history_sequence,
                artifact_materialized = $materialized, reconciliation_json = $reconciliation,
                row_version = $next_version, updated_at = $updated
            WHERE turn_record_id = $id AND row_version = $expected_version AND state = $expected_state;
            """, cancellationToken,
            ("$provider_turn", updated.ProviderTurnId), ("$request", updated.RequestId),
            ("$state", updated.State.ToString()), ("$write_started", updated.WriteStarted),
            ("$submitted", updated.Submitted), ("$accepted", updated.Accepted),
            ("$terminal", updated.Terminal), ("$output", updated.OutputBody), ("$output_hash", updated.OutputHash),
            ("$history_kind", updated.HistoryKind), ("$history_sequence", updated.HistorySequence),
            ("$materialized", updated.ArtifactMaterialized), ("$reconciliation", updated.ReconciliationJson),
            ("$next_version", updated.RowVersion), ("$updated", Format(updated.UpdatedAt)),
            ("$id", expected.TurnRecordId), ("$expected_version", expected.RowVersion),
            ("$expected_state", expected.State.ToString()));
        return changed == 1 ? Success(updated.RowVersion) : Conflict("Decision turn compare-and-swap conflict.");
    }

    private static DecisionSessionActiveState ReadActive(SqliteDataReader reader) => new(
        reader.GetString(0), reader.GetString(1),
        new DecisionSessionAccounting(
            reader.GetInt32(2), reader.GetDouble(3), reader.GetInt32(4), reader.GetDouble(5),
            reader.GetDouble(6), reader.GetDouble(7), reader.GetInt32(8),
            reader.IsDBNull(9) ? null : reader.GetInt32(9), reader.GetInt32(10)),
        reader.GetString(11), NullableString(reader, 12), reader.GetInt64(13), ParseTime(reader.GetString(14)));

    private static DecisionSessionLineageNode ReadLineage(SqliteDataReader reader, int offset, string lineageId) => new(
        lineageId, NullableString(reader, offset), reader.GetString(offset + 1), reader.GetString(offset + 2),
        NullableString(reader, offset + 3), reader.GetString(offset + 4), reader.GetString(offset + 5),
        Enum.Parse<RecoveryCompleteness>(reader.GetString(offset + 6)), NullableString(reader, offset + 7),
        NullableString(reader, offset + 8), NullableString(reader, offset + 9), ParseTime(reader.GetString(offset + 10)),
        reader.IsDBNull(offset + 11) ? null : ParseTime(reader.GetString(offset + 11)),
        reader.IsDBNull(offset + 12) ? null : ParseTime(reader.GetString(offset + 12)), reader.GetString(offset + 13));

    private static async Task<int> ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<long> ScalarLongAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ScalarStringAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameters(command, parameters);
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToString(result, CultureInfo.InvariantCulture);
    }

    private static void AddParameters(SqliteCommand command, IEnumerable<(string Name, object? Value)> parameters)
    {
        foreach ((string name, object? value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }

    private static string Json<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset ParseTime(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    private static string? NullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static RecoveryStoreWriteResult Success(long version) => new(true, false, version, null);
    private static RecoveryStoreWriteResult Conflict(string diagnostic) => new(false, true, null, diagnostic);
}
