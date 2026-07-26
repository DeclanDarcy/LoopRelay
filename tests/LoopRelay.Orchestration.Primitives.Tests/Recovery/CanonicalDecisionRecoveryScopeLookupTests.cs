using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Recovery;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Tests.Recovery;

/// <summary>
/// Pins the semantics of the recovery-scope lookup after it stopped joining on
/// <c>json_extract(document_json, '$.scopeId')</c> and started reading the denormalised, indexed
/// <c>canonical_recovery_action_events.scope_id</c> column (PERF-20): latest-wins-by-event_id,
/// compare-and-swap conflict detection, and the query actually being index-backed.
/// </summary>
public sealed class CanonicalDecisionRecoveryScopeLookupTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public async Task Latest_attempt_per_scope_wins_by_event_id_with_several_events_and_two_scopes()
    {
        Fixture fixture = await Fixture.CreateAsync();
        Scope alpha = await fixture.CreateScopeAsync("scope-alpha");
        Scope beta = await fixture.CreateScopeAsync("scope-beta");

        // Interleave the two scopes' event appends so a lookup that ignored the scope and simply
        // took the globally-latest event would answer with the other scope's attempt.
        RecoveryAttempt alphaPending = await fixture.BeginAttemptAsync(alpha, "attempt-alpha");
        RecoveryAttempt betaPending = await fixture.BeginAttemptAsync(beta, "attempt-beta");
        RecoveryAttempt alphaUnknown = await fixture.AdvanceAsync(
            alphaPending, RecoveryAttemptStatus.UnknownOutcome, Now.AddSeconds(1));
        RecoveryAttempt betaUnknown = await fixture.AdvanceAsync(
            betaPending, RecoveryAttemptStatus.UnknownOutcome, Now.AddSeconds(2));
        RecoveryAttempt alphaFailed = await fixture.AdvanceAsync(
            alphaUnknown, RecoveryAttemptStatus.RecoveryFailed, Now.AddSeconds(3));
        RecoveryAttempt betaFailed = await fixture.AdvanceAsync(
            betaUnknown, RecoveryAttemptStatus.RecoveryFailed, Now.AddSeconds(4));

        Assert.Equal(alphaFailed, await fixture.Store.ReadLatestAttemptAsync(alpha.ScopeId));
        Assert.Equal(betaFailed, await fixture.Store.ReadLatestAttemptAsync(beta.ScopeId));

        // beta's terminal event has the highest event_id of all; alpha must still report its own.
        Assert.NotEqual(betaFailed.AttemptId, (await fixture.Store.ReadLatestAttemptAsync(alpha.ScopeId))!.AttemptId);

        // A scope with no WarmSession recovery case and no events resolves to nothing, exactly as
        // the previous inner join did.
        Assert.Null(await fixture.Store.ReadLatestAttemptAsync("scope-never-recovered"));
    }

    [Fact]
    public async Task Compare_and_swap_still_detects_a_conflicting_attempt_revision()
    {
        Fixture fixture = await Fixture.CreateAsync();
        Scope alpha = await fixture.CreateScopeAsync("scope-alpha");
        RecoveryAttempt pending = await fixture.BeginAttemptAsync(alpha, "attempt-alpha");
        RecoveryAttempt unknown = pending with
        {
            Status = RecoveryAttemptStatus.UnknownOutcome,
            RowVersion = pending.RowVersion + 1,
            UpdatedAt = Now.AddSeconds(1),
        };

        RecoveryStoreWriteResult first = await fixture.Store.CompareAndSwapAttemptAsync(pending, unknown);
        RecoveryStoreWriteResult stale = await fixture.Store.CompareAndSwapAttemptAsync(pending, unknown);

        Assert.True(first.Succeeded);
        Assert.True(stale.Conflict);
        Assert.False(stale.Succeeded);
        Assert.Equal(unknown, await fixture.Store.ReadAttemptAsync(pending.AttemptId));
        Assert.Equal(unknown, await fixture.Store.ReadLatestAttemptAsync(alpha.ScopeId));
    }

    [Fact]
    public async Task Latest_attempt_lookup_is_backed_by_the_recovery_scope_index()
    {
        Fixture fixture = await Fixture.CreateAsync();
        Scope alpha = await fixture.CreateScopeAsync("scope-alpha");
        await fixture.BeginAttemptAsync(alpha, "attempt-alpha");

        IReadOnlyList<string> plan = await fixture.ExplainAsync(
            CanonicalDecisionRecoveryStore.LatestAttemptByScopeSql,
            alpha.ScopeId);

        // `SEARCH ... (scope_id=?)` - a seek on the new index, not a scan of the ledger. The
        // discarded `json_extract` join planned as `SCAN event` + `USE TEMP B-TREE FOR ORDER BY`
        // on this same fixture, so both negative assertions below are load-bearing.
        Assert.Contains(
            plan,
            step => step.Contains("SEARCH event USING INDEX idx_recovery_action_events_scope", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, step => step.Contains("SCAN event", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, step => step.Contains("TEMP B-TREE", StringComparison.Ordinal));
    }

    private sealed record Scope(
        string ScopeId,
        DecisionSessionScopeRecord Record,
        DecisionSessionLineageNode Lineage,
        DecisionSessionActiveState Active);

    private sealed record Fixture(
        Repository Repository,
        SqliteRecoveryStore Continuity,
        CanonicalRecoveryStore Canonical,
        CanonicalDecisionRecoveryStore Store,
        SessionContinuityProfile Profile)
    {
        public static Task<Fixture> CreateAsync()
        {
            string root = Directory.CreateTempSubdirectory("canonical-decision-recovery-scope").FullName;
            var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(root), Path = root };
            var continuity = new SqliteRecoveryStore(repository);
            return Task.FromResult(new Fixture(
                repository,
                continuity,
                new CanonicalRecoveryStore(repository),
                new CanonicalDecisionRecoveryStore(repository, continuity),
                CreateProfile()));
        }

        public async Task<Scope> CreateScopeAsync(string scopeId)
        {
            var record = new DecisionSessionScopeRecord(
                scopeId, "workspace-1", new string('e', 64), new string('f', 64),
                "Decision", "decision-session-scope.v1", "Active", Now, null);
            var lineage = new DecisionSessionLineageNode(
                $"lineage-{scopeId}", scopeId, "codex", $"thread-{scopeId}", null,
                $"lineage-{scopeId}", "Fresh", RecoveryCompleteness.Full, null, Profile.Digest, null,
                Now, Now, null, "Authoritative");
            var active = new DecisionSessionActiveState(
                scopeId, lineage.LineageId,
                new DecisionSessionAccounting(0, 0, 0, 0, 0, 250_000, 0, null, 0),
                "policy.v1", null, 0, Now);
            Assert.True((await Continuity.CreateScopeAndActivateAsync(record, lineage, active, Profile)).Succeeded);

            // The warm-session recovery case is what `CanonicalRecoveryCaseRecorder` writes in
            // production; the lookup only reports attempts for a scope that has one.
            var causality = new CanonicalCausalContext(
                WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
                TransitionRunIdentity.New(), AttemptIdentity.New());
            var subject = new RecoveryCausalSubject(causality, SessionIdentity: scopeId);
            var recoveryCase = new CanonicalRecoveryCase(
                RecoveryCaseIdentity.New(), RecoveryScopeKind.WarmSession, subject, Now);
            await Canonical.AppendCaseAndClassificationAsync(
                recoveryCase,
                new CanonicalRecoveryClassification(
                    RecoveryClassificationIdentity.New(), recoveryCase.Identity,
                    RecoveryBoundaryClassification.Failed, RecoveryCancellationBoundary.None,
                    ["resume-failed"], null, Now),
                CancellationToken.None);
            return new Scope(scopeId, record, lineage, active);
        }

        public async Task<RecoveryAttempt> BeginAttemptAsync(Scope scope, string attemptId)
        {
            RecoveryAttempt attempt = new RecoveryJournal().Begin(
                attemptId, null, scope.ScopeId, scope.Lineage.LineageId, $"run-{scope.ScopeId}",
                Profile.Digest, "ResumeFailure", $"idem-{attemptId}", Now);
            Assert.True((await Store.BeginAttemptAsync(attempt, scope.Active.RowVersion, Profile)).Succeeded);
            return attempt;
        }

        public async Task<RecoveryAttempt> AdvanceAsync(
            RecoveryAttempt expected,
            RecoveryAttemptStatus status,
            DateTimeOffset at)
        {
            RecoveryAttempt updated = expected with
            {
                Status = status,
                RowVersion = expected.RowVersion + 1,
                UpdatedAt = at,
            };
            Assert.True((await Store.CompareAndSwapAttemptAsync(expected, updated)).Succeeded);
            return updated;
        }

        public async Task<IReadOnlyList<string>> ExplainAsync(string sql, string scopeId)
        {
            await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(
                LoopRelayWorkspaceDatabase.Resolve(Repository));
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"EXPLAIN QUERY PLAN {sql}";
            command.Parameters.AddWithValue("$scope", scopeId);
            List<string> steps = [];
            await using SqliteDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                steps.Add(reader.GetString(reader.GetOrdinal("detail")));
            }

            return steps;
        }

        private static SessionContinuityProfile CreateProfile()
        {
            var operations = new Dictionary<SessionContinuityOperation, SessionOperationSupportDescriptor>
            {
                [SessionContinuityOperation.Resume] = new SessionOperationSupportDescriptor(
                    SessionOperationSupport.Supported, "v2",
                    new Dictionary<string, SessionParameterSupport>
                    {
                        [SessionContinuityProfile.ExcludeTurnsParameter] = new(SessionOperationSupport.Supported, "test"),
                    },
                    "load", "same-id", "none", "read", "test"),
            };
            return new SessionContinuityProfile(
                "codex", "test", "0.142.5", "codex", "app-server-v2", "schema",
                new Dictionary<string, bool> { ["experimentalApi"] = true },
                new Dictionary<string, string>(), operations, 256_000, "test", "test",
                negotiatedAt: DateTimeOffset.UnixEpoch);
        }
    }
}
