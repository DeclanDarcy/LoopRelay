using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Recovery;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Tests.Recovery;

public sealed class SqliteRecoveryStoreTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public async Task CompareAndSwapRejectsAStaleAttemptVersion()
    {
        Fixture fixture = await Fixture.CreateAsync();
        RecoveryJournal journal = new();
        RecoveryAttempt pending = await fixture.BeginAttemptAsync(journal);
        RecoveryAttempt preparing = journal.RecordResumeFailure(
            pending, Failure("UnavailableSession", fixture.Profile.Digest), true, Now.AddSeconds(1)).Attempt;

        RecoveryStoreWriteResult first = await fixture.Store.CompareAndSwapAttemptAsync(pending, preparing);
        RecoveryStoreWriteResult stale = await fixture.Store.CompareAndSwapAttemptAsync(pending, preparing);

        Assert.True(first.Succeeded);
        Assert.True(stale.Conflict);
        Assert.Equal(preparing, await fixture.Store.ReadAttemptAsync(preparing.AttemptId));
    }

    [Fact]
    public async Task RecoveryActivationAtomicallySwitchesAuthorityAndCompletesAttempt()
    {
        Fixture fixture = await Fixture.CreateAsync();
        RecoveryJournal journal = new();
        RecoveryAttempt pending = await fixture.BeginAttemptAsync(journal);
        RecoveryAttempt preparing = journal.RecordResumeFailure(
            pending, Failure("UnavailableSession", fixture.Profile.Digest), true, Now.AddSeconds(1)).Attempt;
        Assert.True((await fixture.Store.CompareAndSwapAttemptAsync(pending, preparing)).Succeeded);

        RecoveryPlan plan = RecoveryPlanTests.Plan(profileDigest: fixture.Profile.Digest);
        RecoveryAttempt planned = journal.RecordPlan(preparing, plan, Now.AddSeconds(2)).Attempt;
        Assert.True((await fixture.Store.RecordPlanAsync(preparing, planned, plan)).Succeeded);

        RecoveryAttempt creating = journal.BeginReplacement(planned, Now.AddSeconds(3)).Attempt;
        Assert.True((await fixture.Store.CompareAndSwapAttemptAsync(planned, creating)).Succeeded);

        DecisionSessionLineageNode child = new(
            "lineage-child", fixture.Scope.ScopeId, "codex", "thread-child", fixture.Lineage.LineageId,
            fixture.Lineage.RootLineageId, plan.Mechanism.Identity, plan.ExpectedCompleteness,
            plan.Sources[0].Digest, fixture.Profile.Digest, plan.Digest, Now.AddSeconds(4), null, null, "Inactive");
        RecoveryAttempt created = journal.RecordReplacementCreated(
            creating, child.LineageId, "request-1", "correlation-1", Now.AddSeconds(4)).Attempt;
        Assert.True((await fixture.Store.RecordReplacementAsync(creating, created, child)).Succeeded);

        RecoveryAttempt contextPending = journal.RecordContextPending(created, Now.AddSeconds(5)).Attempt;
        Assert.True((await fixture.Store.CompareAndSwapAttemptAsync(created, contextPending)).Succeeded);
        RecoveryAttempt completed = journal.Complete(contextPending, Now.AddSeconds(6)).Attempt;

        RecoveryStoreWriteResult activation = await fixture.Store.CompleteRecoveryAndActivateAsync(
            contextPending, completed, fixture.Active, child);

        Assert.True(activation.Succeeded);
        ActiveStateReadResult active = await fixture.Store.ReadActiveAsync(fixture.Scope.ScopeId);
        Assert.Equal(ActiveStateReadStatus.Present, active.Status);
        Assert.Equal(child.LineageId, active.Active!.LineageId);
        Assert.Equal("Authoritative", active.Lineage!.AuthorityState);
        Assert.Equal(RecoveryAttemptStatus.RecoveryCompleted,
            (await fixture.Store.ReadAttemptAsync(completed.AttemptId))!.Status);
    }

    [Fact]
    public async Task ActivationConflictLeavesOriginalAuthoritativeAndAttemptNonterminal()
    {
        Fixture fixture = await Fixture.CreateAsync();
        RecoveryJournal journal = new();
        RecoveryAttempt pending = await fixture.BeginAttemptAsync(journal);
        RecoveryAttempt preparing = journal.RecordResumeFailure(
            pending, Failure("UnavailableSession", fixture.Profile.Digest), true, Now.AddSeconds(1)).Attempt;
        await fixture.Store.CompareAndSwapAttemptAsync(pending, preparing);
        RecoveryPlan plan = RecoveryPlanTests.Plan(profileDigest: fixture.Profile.Digest);
        RecoveryAttempt planned = journal.RecordPlan(preparing, plan, Now.AddSeconds(2)).Attempt;
        await fixture.Store.RecordPlanAsync(preparing, planned, plan);
        RecoveryAttempt creating = journal.BeginReplacement(planned, Now.AddSeconds(3)).Attempt;
        await fixture.Store.CompareAndSwapAttemptAsync(planned, creating);
        DecisionSessionLineageNode child = new(
            "lineage-child-conflict", fixture.Scope.ScopeId, "codex", "thread-child-conflict",
            fixture.Lineage.LineageId, fixture.Lineage.RootLineageId, plan.Mechanism.Identity,
            plan.ExpectedCompleteness, plan.Sources[0].Digest, fixture.Profile.Digest, plan.Digest,
            Now.AddSeconds(4), null, null, "Inactive");
        RecoveryAttempt created = journal.RecordReplacementCreated(creating, child.LineageId, null, null, Now.AddSeconds(4)).Attempt;
        await fixture.Store.RecordReplacementAsync(creating, created, child);
        RecoveryAttempt context = journal.RecordContextPending(created, Now.AddSeconds(5)).Attempt;
        await fixture.Store.CompareAndSwapAttemptAsync(created, context);
        RecoveryAttempt complete = journal.Complete(context, Now.AddSeconds(6)).Attempt;
        DecisionSessionActiveState stale = fixture.Active with { RowVersion = 999 };

        RecoveryStoreWriteResult result = await fixture.Store.CompleteRecoveryAndActivateAsync(context, complete, stale, child);

        Assert.True(result.Conflict);
        ActiveStateReadResult active = await fixture.Store.ReadActiveAsync(fixture.Scope.ScopeId);
        Assert.Equal(fixture.Lineage.LineageId, active.Active!.LineageId);
        Assert.Equal(RecoveryAttemptStatus.ContextInjectionPending,
            (await fixture.Store.ReadAttemptAsync(context.AttemptId))!.Status);
    }

    [Fact]
    public async Task V1LegacyResumeIsImportedAsUnscopedNonAuthoritativeLineage()
    {
        Repository repository = RepositoryAtTemp();
        string path = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(path);
        await connection.OpenAsync();
        await using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                CREATE TABLE workspace_metadata(key text primary key, value text not null);
                CREATE TABLE decision_session_resume(
                    id integer primary key check (id = 1), document_json text not null, saved_at text not null);
                INSERT INTO schema_metadata VALUES ('schema_version','1');
                INSERT INTO workspace_metadata VALUES ('persistence_state','imported');
                INSERT INTO decision_session_resume VALUES (
                    1, '{"schemaVersion":1,"threadId":"thread-legacy"}', '2026-01-01T00:00:00Z');
                """;
            await command.ExecuteNonQueryAsync();
        }

        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);

        Assert.Equal("LegacyScopeUnverified", await ScalarAsync(connection,
            "SELECT parse_status FROM decision_session_legacy_imports LIMIT 1;"));
        Assert.Equal("LegacyScopeUnverified", await ScalarAsync(connection,
            "SELECT authority_state FROM decision_session_lineage WHERE provider_session_id = 'thread-legacy';"));
        Assert.Equal("0", await ScalarAsync(connection, "SELECT count(*) FROM decision_session_active;"));
    }

    [Fact]
    public async Task DecisionOutputHistoryAccountingAndCorrelationCommitAtomically()
    {
        Fixture fixture = await Fixture.CreateAsync();
        DecisionSessionTurnRecord pending = Turn(fixture);
        Assert.True((await fixture.Store.BeginDecisionTurnAsync(pending)).Succeeded);
        DecisionSessionTurnRecord terminal = pending with
        {
            State = DecisionTurnState.Terminal,
            WriteStarted = true,
            Submitted = true,
            Accepted = true,
            Terminal = true,
            ProviderTurnId = "turn-1",
            RowVersion = 1,
            UpdatedAt = Now.AddSeconds(1),
        };
        Assert.True((await fixture.Store.CompareAndSwapDecisionTurnAsync(pending, terminal)).Succeeded);
        DecisionSessionAccounting accounting = fixture.Active.Accounting with
        {
            OccupancyTokens = 42,
            ReuseCycles = 1,
        };

        DecisionTurnCommitResult commit = await fixture.Store.CommitDecisionOutputAsync(
            terminal, fixture.Active, accounting, "# Decisions\n\nDo it.", "policy.v2");

        Assert.True(commit.Write.Succeeded);
        Assert.Equal(DecisionTurnState.Committed, commit.Turn!.State);
        Assert.Equal(1, commit.Turn.HistorySequence);
        Assert.Equal(42, commit.Active!.Accounting.OccupancyTokens);
        Assert.Equal(1, commit.Active.RowVersion);
        Assert.True((await fixture.Store.MarkDecisionArtifactMaterializedAsync(commit.Turn)).Succeeded);

        string database = LoopRelayWorkspaceDatabase.Resolve(fixture.Repository);
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(database);
        await connection.OpenAsync();
        Assert.Equal("run-turn-1", await ScalarAsync(connection,
            "SELECT producer_run_id FROM loop_history WHERE kind = 'decisions' AND sequence = 1;"));
        Assert.Equal("turn-1", await ScalarAsync(connection,
            "SELECT provider_turn_id FROM session_transition_correlations WHERE transition_run_id = 'run-turn-1';"));
    }

    [Fact]
    public async Task UncertainDecisionTurnBlocksDuplicateIntentAndPreservesNoOutput()
    {
        Fixture fixture = await Fixture.CreateAsync();
        DecisionSessionTurnRecord pending = Turn(fixture);
        await fixture.Store.BeginDecisionTurnAsync(pending);
        DecisionSessionTurnRecord unknown = pending with
        {
            State = DecisionTurnState.Unknown,
            WriteStarted = true,
            Submitted = true,
            RowVersion = 1,
            UpdatedAt = Now.AddSeconds(1),
        };
        await fixture.Store.CompareAndSwapDecisionTurnAsync(pending, unknown);

        RecoveryStoreWriteResult duplicate = await fixture.Store.BeginDecisionTurnAsync(
            pending with { TurnRecordId = "different-turn" });

        Assert.True(duplicate.Conflict);
        DecisionSessionTurnRecord stored = (await fixture.Store.ReadDecisionTurnAsync("run-turn-1", "input-hash-1"))!;
        Assert.Equal(DecisionTurnState.Unknown, stored.State);
        Assert.Null(stored.OutputBody);
    }

    [Fact]
    public async Task PlannedTransferKeepsOriginalActiveUntilSuccessorProposalCommits()
    {
        Fixture fixture = await Fixture.CreateAsync();
        var successor = new DecisionSessionLineageNode(
            "lineage-planned", fixture.Scope.ScopeId, "codex", "thread-planned",
            fixture.Lineage.LineageId, fixture.Lineage.RootLineageId, "PlannedTransfer",
            RecoveryCompleteness.Full, null, fixture.Profile.Digest, null,
            Now.AddSeconds(1), null, null, "Inactive");

        Assert.True((await fixture.Store.RecordPlannedSuccessorAsync(fixture.Active, successor)).Succeeded);
        ActiveStateReadResult before = await fixture.Store.ReadActiveAsync(fixture.Scope.ScopeId);
        Assert.Equal(fixture.Lineage.LineageId, before.Active!.LineageId);
        Assert.Equal("Authoritative", before.Lineage!.AuthorityState);

        var pending = new DecisionSessionTurnRecord(
            "turn-planned", fixture.Scope.ScopeId, successor.LineageId, "run-planned", "input-planned",
            successor.ProviderSessionId, null, null, DecisionTurnState.Pending,
            false, false, false, false, null, null, null, null, false, null, 0,
            Now.AddSeconds(2), Now.AddSeconds(2));
        Assert.True((await fixture.Store.BeginDecisionTurnAsync(pending)).Succeeded);
        DecisionSessionTurnRecord terminal = pending with
        {
            State = DecisionTurnState.Terminal,
            WriteStarted = true,
            Submitted = true,
            Accepted = true,
            Terminal = true,
            ProviderTurnId = "provider-turn-planned",
            RowVersion = 1,
            UpdatedAt = Now.AddSeconds(3),
        };
        Assert.True((await fixture.Store.CompareAndSwapDecisionTurnAsync(pending, terminal)).Succeeded);

        DecisionTurnCommitResult committed = await fixture.Store.CommitDecisionOutputAsync(
            terminal, fixture.Active, fixture.Active.Accounting, "planned output", "policy");

        Assert.True(committed.Write.Succeeded);
        Assert.Equal(successor.LineageId, committed.Active!.LineageId);
        ActiveStateReadResult after = await fixture.Store.ReadActiveAsync(fixture.Scope.ScopeId);
        Assert.Equal(successor.LineageId, after.Active!.LineageId);
        Assert.Equal("Authoritative", after.Lineage!.AuthorityState);
        Assert.Equal("Superseded", (await fixture.Store.ReadLineageAsync(fixture.Lineage.LineageId))!.AuthorityState);
    }

    private static RecoveryFailure Failure(string classification, string profileDigest) =>
        new(classification, "thread/resume", null, profileDigest, "redacted", false);

    private static DecisionSessionTurnRecord Turn(Fixture fixture) => new(
        "turn-record-1", fixture.Scope.ScopeId, fixture.Lineage.LineageId, "run-turn-1", "input-hash-1",
        fixture.Lineage.ProviderSessionId, null, null, DecisionTurnState.Pending,
        false, false, false, false, null, null, null, null, false, null, 0, Now, Now);

    private static Repository RepositoryAtTemp()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-recovery-store-").FullName;
        return new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(path), Path = path };
    }

    private static async Task<string?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync());
    }

    private sealed record Fixture(
        Repository Repository,
        SqliteRecoveryStore Store,
        SessionContinuityProfile Profile,
        DecisionSessionScopeRecord Scope,
        DecisionSessionLineageNode Lineage,
        DecisionSessionActiveState Active)
    {
        public static async Task<Fixture> CreateAsync()
        {
            Repository repository = RepositoryAtTemp();
            var store = new SqliteRecoveryStore(repository);
            SessionContinuityProfile profile = CreateProfile();
            var scope = new DecisionSessionScopeRecord(
                "scope-1", "workspace-1", new string('e', 64), new string('f', 64),
                "Decision", "decision-session-scope.v1", "Active", Now, null);
            var lineage = new DecisionSessionLineageNode(
                "lineage-original", scope.ScopeId, "codex", "thread-original", null,
                "lineage-original", "Fresh", RecoveryCompleteness.Full, null, profile.Digest, null,
                Now, Now, null, "Authoritative");
            var active = new DecisionSessionActiveState(
                scope.ScopeId, lineage.LineageId,
                new DecisionSessionAccounting(0, 0, 0, 0, 0, 250_000, 0, null, 0),
                "policy.v1", null, 0, Now);
            RecoveryStoreWriteResult result = await store.CreateScopeAndActivateAsync(scope, lineage, active, profile);
            Assert.True(result.Succeeded);
            return new Fixture(repository, store, profile, scope, lineage, active);
        }

        public async Task<RecoveryAttempt> BeginAttemptAsync(RecoveryJournal journal)
        {
            RecoveryAttempt attempt = journal.Begin(
                "attempt-1", null, Scope.ScopeId, Lineage.LineageId, "run-1", Profile.Digest,
                "ResumeFailure", "idem-attempt-1", Now);
            RecoveryStoreWriteResult result = await Store.BeginAttemptAsync(attempt, Active.RowVersion, Profile);
            Assert.True(result.Succeeded);
            return attempt;
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
