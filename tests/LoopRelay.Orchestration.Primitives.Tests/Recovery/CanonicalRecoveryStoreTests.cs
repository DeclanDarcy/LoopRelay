using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Runtime;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Orchestration.Tests.Recovery;

public sealed class CanonicalRecoveryStoreTests
{
    [Fact]
    public async Task Canonical_case_classification_plan_and_action_journal_round_trip_across_restart()
    {
        Repository repository = RepositoryFixture();
        var store = new CanonicalRecoveryStore(repository);
        CanonicalRecoveryCase recoveryCase = Case();
        CanonicalRecoveryClassification first = Classification(recoveryCase, RecoveryBoundaryClassification.AcceptedUnknown);
        await store.AppendCaseAndClassificationAsync(recoveryCase, first, CancellationToken.None);
        CanonicalRecoveryClassification second = Classification(
            recoveryCase,
            RecoveryBoundaryClassification.ProviderUnknown,
            first.Identity);
        await store.AppendClassificationAsync(second, CancellationToken.None);
        var plan = new CanonicalRecoveryPlan(
            RecoveryPlanIdentity.New(), recoveryCase.Identity, second.Identity,
            CanonicalRecoveryAction.ReconcileProvider, "policy-1", "profile-1", ["dispatch-1"],
            ["correlation exists"], ["provider outcome classified"], "recovery-idem-1", null,
            DateTimeOffset.Parse("2026-07-12T00:00:00Z"));
        await store.AppendPlanAsync(plan, CancellationToken.None);
        RecoveryActionIdentity action = RecoveryActionIdentity.New();
        await store.AppendActionEventAsync(new CanonicalRecoveryActionEvent(
            action, plan.Identity, RecoveryActionLifecycle.Started, "started", ["dispatch-1"],
            DateTimeOffset.Parse("2026-07-12T00:00:01Z")), CancellationToken.None);
        await store.AppendActionEventAsync(new CanonicalRecoveryActionEvent(
            action, plan.Identity, RecoveryActionLifecycle.Succeeded, "reconciled", ["turn-1"],
            DateTimeOffset.Parse("2026-07-12T00:00:02Z")), CancellationToken.None);

        var restarted = new CanonicalRecoveryStore(repository);

        CanonicalRecoveryCase restoredCase = Assert.IsType<CanonicalRecoveryCase>(
            await restarted.ReadCaseAsync(recoveryCase.Identity, CancellationToken.None));
        Assert.Equal(recoveryCase.Identity, restoredCase.Identity);
        Assert.Equal(recoveryCase.Subject, restoredCase.Subject);
        CanonicalRecoveryClassification restoredClassification = Assert.IsType<CanonicalRecoveryClassification>(
            await restarted.ReadLatestClassificationAsync(recoveryCase.Identity, CancellationToken.None));
        Assert.Equal(second.Identity, restoredClassification.Identity);
        Assert.Equal(second.Classification, restoredClassification.Classification);
        Assert.Equal(second.SourceEvidence, restoredClassification.SourceEvidence);
        CanonicalRecoveryPlan restoredPlan = Assert.IsType<CanonicalRecoveryPlan>(
            await restarted.ReadPlanAsync(plan.Identity, CancellationToken.None));
        Assert.Equal(plan.Identity, restoredPlan.Identity);
        Assert.Equal(plan.Action, restoredPlan.Action);
        Assert.Equal(plan.SourceEvidence, restoredPlan.SourceEvidence);
        Assert.Equal(plan.Identity, (await restarted.ReadPlanByIdempotencyKeyAsync(
            plan.IdempotencyKey, CancellationToken.None))?.Identity);
        Assert.Equal(
            [RecoveryActionLifecycle.Started, RecoveryActionLifecycle.Succeeded],
            (await restarted.ReadActionEventsAsync(plan.Identity, CancellationToken.None))
                .Select(item => item.Lifecycle).ToArray());
    }

    [Fact]
    public async Task Reclassification_rejects_a_stale_supersedes_identity()
    {
        Repository repository = RepositoryFixture();
        var store = new CanonicalRecoveryStore(repository);
        CanonicalRecoveryCase recoveryCase = Case();
        CanonicalRecoveryClassification first = Classification(recoveryCase, RecoveryBoundaryClassification.InFlight);
        await store.AppendCaseAndClassificationAsync(recoveryCase, first, CancellationToken.None);

        CanonicalRecoveryClassification stale = Classification(
            recoveryCase,
            RecoveryBoundaryClassification.AcceptedUnknown,
            RecoveryClassificationIdentity.New());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.AppendClassificationAsync(stale, CancellationToken.None));
    }

    [Fact]
    public async Task Version_ten_transition_recovery_plan_migrates_to_v14_without_losing_legacy_identity()
    {
        Repository repository = RepositoryFixture();
        string path = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(path);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        foreach (string table in new[]
        {
            "canonical_recovery_action_events", "canonical_recovery_source_links",
            "canonical_recovery_plans", "canonical_recovery_classifications", "canonical_recovery_cases",
        })
            await ExecuteAsync(connection, $"DROP TABLE {table};");
        await ExecuteAsync(connection,
            "UPDATE schema_metadata SET value = '10' WHERE key = 'schema_version';");
        await ExecuteAsync(connection,
            "UPDATE schema_metadata SET value = $shape WHERE key = 'schema_shape';",
            ("$shape", LoopRelayWorkspaceDatabase.CanonicalV10ShapeFingerprint));
        await ExecuteAsync(connection, """
            INSERT INTO transition_recovery_plans (
                recovery_id, transition_run_id, source_attempt_id, classification, action,
                resulting_attempt_mode, next_attempt_index, evidence_json, preconditions_json, planned_at
            ) VALUES (
                'legacy-recovery-1', 'transition-1', 'attempt-1', 'ReconcileProvider',
                'ReconcileProviderOutcome', 'NoAttempt', 1, '["dispatch-1"]',
                '["provider correlation is available"]', '2026-07-12T00:00:00Z'
            );
            """);
        await ExecuteAsync(connection, "PRAGMA foreign_keys = OFF;");
        await ExecuteAsync(connection, """
            INSERT INTO session_recovery_attempts (
                attempt_id, previous_attempt_id, scope_id, original_lineage_id,
                replacement_lineage_id, transition_run_id, status, row_version,
                profile_digest, plan_digest, failure_classification, failure_json,
                trigger, mechanism_identity, mechanism_version, idempotency_key,
                provider_request_id, provider_correlation_id, retry_count, diagnostic_json,
                created_at, updated_at, completed_at
            ) VALUES (
                'legacy-session-attempt-1', NULL, 'legacy-scope', 'legacy-lineage', NULL,
                'transition-1', 'UnknownOutcome', 0, 'legacy-profile', NULL,
                'UnknownOutcome', '{}', 'restart', NULL, NULL, 'legacy-session-idem-1',
                NULL, NULL, 0, '["provider-outcome-unknown"]',
                '2026-07-12T00:00:00Z', '2026-07-12T00:00:01Z', NULL
            );
            """);
        await ExecuteAsync(connection, "PRAGMA foreign_keys = ON;");

        WorkspaceSchemaInspection before = await LoopRelayWorkspaceDatabase.InspectSchemaAsync(connection);
        Assert.Equal(WorkspaceSchemaShape.CanonicalV10Complete, before.Shape);
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal("15", await ScalarAsync(connection,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.Equal("legacy-recovery-1", await ScalarAsync(connection,
            "SELECT case_id FROM canonical_recovery_cases WHERE case_id = 'legacy-recovery-1';"));
        Assert.Equal("legacy-recovery-1", await ScalarAsync(connection,
            "SELECT plan_id FROM canonical_recovery_plans WHERE plan_id = 'legacy-recovery-1';"));
        Assert.Equal("ReconcileProvider", await ScalarAsync(connection,
            "SELECT action FROM canonical_recovery_plans WHERE plan_id = 'legacy-recovery-1';"));
        Assert.Equal("legacy-session-attempt-1", await ScalarAsync(connection,
            "SELECT case_id FROM canonical_recovery_cases WHERE case_id = 'legacy-session-attempt-1';"));
        Assert.Equal("ProviderUnknown", await ScalarAsync(connection,
            "SELECT classification FROM canonical_recovery_classifications WHERE case_id = 'legacy-session-attempt-1';"));
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
        Assert.Equal("1", await ScalarAsync(connection,
            "SELECT count(*) FROM canonical_recovery_cases WHERE case_id = 'legacy-recovery-1';"));
    }

    private static CanonicalRecoveryCase Case()
    {
        var causality = new CanonicalCausalContext(
            WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(), AttemptIdentity.New());
        return new CanonicalRecoveryCase(
            RecoveryCaseIdentity.New(), RecoveryScopeKind.ProviderDispatch,
            new RecoveryCausalSubject(causality, SessionIdentity: "session-1", TurnIdentity: "turn-1"),
            DateTimeOffset.Parse("2026-07-12T00:00:00Z"));
    }

    private static CanonicalRecoveryClassification Classification(
        CanonicalRecoveryCase recoveryCase,
        RecoveryBoundaryClassification classification,
        RecoveryClassificationIdentity? supersedes = null) => new(
            RecoveryClassificationIdentity.New(), recoveryCase.Identity, classification,
            RecoveryCancellationBoundary.None, ["dispatch-1"], supersedes,
            DateTimeOffset.Parse("2026-07-12T00:00:00Z").AddSeconds(supersedes is null ? 0 : 1));

    private static Repository RepositoryFixture()
    {
        string root = Directory.CreateTempSubdirectory("canonical-recovery-store").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(root), Path = root };
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object? value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
