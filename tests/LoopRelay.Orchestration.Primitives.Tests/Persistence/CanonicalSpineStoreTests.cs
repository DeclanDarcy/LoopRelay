using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;

namespace LoopRelay.Orchestration.Tests.Persistence;

public sealed class CanonicalSpineStoreTests
{
    [Fact]
    public async Task Run_rows_round_trip_ordered_by_start_and_upsert_updates_in_place()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await store.UpsertRunAsync(new RunRecord(
            "run_later",
            "ws_workspace",
            "TraditionalRoadmapChain",
            "Run",
            "Active",
            now.AddMinutes(5),
            null,
            null,
            ""));
        await store.UpsertRunAsync(new RunRecord(
            "run_earlier",
            "ws_workspace",
            "TraditionalRoadmapChain",
            "Run",
            "Active",
            now,
            null,
            null,
            ""));

        IReadOnlyList<RunRecord> initial = await store.ReadRunsAsync();

        Assert.Equal(2, initial.Count);
        Assert.Equal("run_earlier", initial[0].RunId);
        Assert.Equal("run_later", initial[1].RunId);
        Assert.Equal("ws_workspace", initial[0].WorkspaceId);
        Assert.Equal("TraditionalRoadmapChain", initial[0].ChainIdentity);
        Assert.Equal("Run", initial[0].InvocationMode);
        Assert.Equal("Active", initial[0].Status);
        Assert.Equal(now, initial[0].StartedAt);
        Assert.Null(initial[0].CompletedAt);
        Assert.Null(initial[0].StopReason);

        await store.UpsertRunAsync(new RunRecord(
            "run_earlier",
            "ws_workspace",
            "TraditionalRoadmapChain",
            "Run",
            "Waiting",
            now,
            now.AddMinutes(2),
            "Waiting",
            "waiting on human review"));

        IReadOnlyList<RunRecord> updated = await store.ReadRunsAsync();

        Assert.Equal(2, updated.Count);
        Assert.Equal("Waiting", updated[0].Status);
        Assert.Equal(now.AddMinutes(2), updated[0].CompletedAt);
        Assert.Equal("Waiting", updated[0].StopReason);
        Assert.Equal("waiting on human review", updated[0].Explanation);
        Assert.Equal("Active", updated[1].Status);
    }

    [Fact]
    public async Task Interrupt_lingering_active_runs_flips_only_active_rows_of_other_runs()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await store.UpsertRunAsync(new RunRecord(
            "run_lingering", "ws_workspace", "TraditionalRoadmapChain", "Run", "Active", now, null, null, ""));
        await store.UpsertRunAsync(new RunRecord(
            "run_finished", "ws_workspace", "TraditionalRoadmapChain", "Run", "Waiting",
            now.AddMinutes(1), now.AddMinutes(1), "Waiting", ""));
        await store.UpsertRunAsync(new RunRecord(
            "run_current", "ws_workspace", "TraditionalRoadmapChain", "Run", "Active", now.AddMinutes(2), null, null, ""));

        await store.InterruptLingeringActiveRunsAsync("run_current");

        IReadOnlyList<RunRecord> runs = await store.ReadRunsAsync();
        RunRecord lingering = Assert.Single(runs, run => run.RunId == "run_lingering");
        Assert.Equal("Interrupted", lingering.Status);
        Assert.NotNull(lingering.CompletedAt);
        RunRecord finished = Assert.Single(runs, run => run.RunId == "run_finished");
        Assert.Equal("Waiting", finished.Status);
        Assert.Equal(now.AddMinutes(1), finished.CompletedAt);
        RunRecord current = Assert.Single(runs, run => run.RunId == "run_current");
        Assert.Equal("Active", current.Status);
        Assert.Null(current.CompletedAt);
    }

    [Fact]
    public async Task Interrupt_lingering_active_runs_flips_instances_and_open_attempts_of_other_runs()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await store.UpsertRunAsync(new RunRecord(
            "run_lingering", "ws_workspace", "TraditionalRoadmapChain", "Run", "Active", now, null, null, ""));
        await store.UpsertRunAsync(new RunRecord(
            "run_current", "ws_workspace", "TraditionalRoadmapChain", "Run", "Active", now.AddMinutes(1), null, null, ""));
        await store.UpsertWorkflowInstanceAsync(new WorkflowInstanceRecord(
            "wfi_lingering", "run_lingering", WorkflowIdentity.Plan, "", "Active", now, null, null));
        await store.UpsertWorkflowInstanceAsync(new WorkflowInstanceRecord(
            "wfi_completed", "run_lingering", WorkflowIdentity.Plan, "", "Completed", now, now.AddMinutes(1), "Completed"));
        await store.UpsertWorkflowInstanceAsync(new WorkflowInstanceRecord(
            "wfi_current", "run_current", WorkflowIdentity.Plan, "", "Active", now.AddMinutes(1), null, null));
        await store.UpsertAttemptAsync(new AttemptRecord(
            "att_open", "tr_001", "wfi_lingering", "run_lingering", 1, now, null, null));
        await store.UpsertAttemptAsync(new AttemptRecord(
            "att_done", "tr_002", "wfi_completed", "run_lingering", 1, now, now.AddMinutes(1), "Completed"));
        await store.UpsertAttemptAsync(new AttemptRecord(
            "att_current", "tr_003", "wfi_current", "run_current", 1, now.AddMinutes(1), null, null));

        await store.InterruptLingeringActiveRunsAsync("run_current");

        RunRecord lingeringRun = Assert.Single(await store.ReadRunsAsync(), run => run.RunId == "run_lingering");
        Assert.Equal("Interrupted", lingeringRun.Status);
        Assert.Equal("Interrupted", lingeringRun.StopReason);
        Assert.NotNull(lingeringRun.CompletedAt);

        IReadOnlyList<WorkflowInstanceRecord> instances = await store.ReadWorkflowInstancesAsync();
        WorkflowInstanceRecord lingeringInstance = Assert.Single(instances, instance => instance.WorkflowInstanceId == "wfi_lingering");
        Assert.Equal("Interrupted", lingeringInstance.Status);
        Assert.NotNull(lingeringInstance.CompletedAt);
        WorkflowInstanceRecord completedInstance = Assert.Single(instances, instance => instance.WorkflowInstanceId == "wfi_completed");
        Assert.Equal("Completed", completedInstance.Status);
        Assert.Equal(now.AddMinutes(1), completedInstance.CompletedAt);
        WorkflowInstanceRecord currentInstance = Assert.Single(instances, instance => instance.WorkflowInstanceId == "wfi_current");
        Assert.Equal("Active", currentInstance.Status);
        Assert.Null(currentInstance.CompletedAt);

        IReadOnlyList<AttemptRecord> attempts = await store.ReadAttemptsAsync();
        AttemptRecord openAttempt = Assert.Single(attempts, attempt => attempt.AttemptId == "att_open");
        Assert.Equal("Interrupted", openAttempt.Outcome);
        Assert.NotNull(openAttempt.CompletedAt);
        AttemptRecord doneAttempt = Assert.Single(attempts, attempt => attempt.AttemptId == "att_done");
        Assert.Equal("Completed", doneAttempt.Outcome);
        Assert.Equal(now.AddMinutes(1), doneAttempt.CompletedAt);
        AttemptRecord currentAttempt = Assert.Single(attempts, attempt => attempt.AttemptId == "att_current");
        Assert.Null(currentAttempt.Outcome);
        Assert.Null(currentAttempt.CompletedAt);
    }

    [Fact]
    public async Task Workflow_instance_rows_round_trip_and_upsert_updates_in_place()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await store.UpsertWorkflowInstanceAsync(new WorkflowInstanceRecord(
            "wfi_001",
            "run_001",
            WorkflowIdentity.Plan,
            "",
            "Active",
            now,
            null,
            null));

        WorkflowInstanceRecord instance = Assert.Single(await store.ReadWorkflowInstancesAsync());
        Assert.Equal("wfi_001", instance.WorkflowInstanceId);
        Assert.Equal("run_001", instance.RunId);
        Assert.Equal(WorkflowIdentity.Plan, instance.Workflow);
        Assert.Equal("", instance.CatalogVersion);
        Assert.Equal("Active", instance.Status);
        Assert.Equal(now, instance.StartedAt);
        Assert.Null(instance.CompletedAt);
        Assert.Null(instance.Outcome);

        await store.UpsertWorkflowInstanceAsync(new WorkflowInstanceRecord(
            "wfi_001",
            "run_001",
            WorkflowIdentity.Plan,
            "",
            "Stopped",
            now,
            now.AddMinutes(3),
            "Waiting"));

        WorkflowInstanceRecord updated = Assert.Single(await store.ReadWorkflowInstancesAsync());
        Assert.Equal("Stopped", updated.Status);
        Assert.Equal(now.AddMinutes(3), updated.CompletedAt);
        Assert.Equal("Waiting", updated.Outcome);
    }

    [Fact]
    public async Task Attempt_rows_round_trip_and_duplicate_transition_run_attempt_index_throws()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await store.UpsertAttemptAsync(new AttemptRecord(
            "att_001",
            "tr_001",
            "wfi_001",
            "run_001",
            1,
            now,
            null,
            null));

        AttemptRecord attempt = Assert.Single(await store.ReadAttemptsAsync());
        Assert.Equal("att_001", attempt.AttemptId);
        Assert.Equal("tr_001", attempt.TransitionRunId);
        Assert.Equal("wfi_001", attempt.WorkflowInstanceId);
        Assert.Equal("run_001", attempt.RunId);
        Assert.Equal(1, attempt.AttemptIndex);
        Assert.Equal(now, attempt.StartedAt);
        Assert.Null(attempt.CompletedAt);
        Assert.Null(attempt.Outcome);

        await store.UpsertAttemptAsync(new AttemptRecord(
            "att_001",
            "tr_001",
            "wfi_001",
            "run_001",
            1,
            now,
            now.AddMinutes(1),
            "Completed"));

        AttemptRecord updated = Assert.Single(await store.ReadAttemptsAsync());
        Assert.Equal(now.AddMinutes(1), updated.CompletedAt);
        Assert.Equal("Completed", updated.Outcome);

        await Assert.ThrowsAsync<SqliteException>(() => store.UpsertAttemptAsync(new AttemptRecord(
            "att_002",
            "tr_001",
            "wfi_001",
            "run_001",
            1,
            now.AddMinutes(2),
            null,
            null)));
    }

    [Fact]
    public async Task Agent_session_rows_round_trip_with_nullable_parents()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        string legacyGuid = Guid.NewGuid().ToString("D");

        await store.UpsertAgentSessionAsync(new AgentSessionRecord(
            "ses_001",
            null,
            null,
            "codex",
            null,
            "Decision",
            legacyGuid,
            now,
            null));

        AgentSessionRecord session = Assert.Single(await store.ReadAgentSessionsAsync());
        Assert.Equal("ses_001", session.SessionId);
        Assert.Null(session.AttemptId);
        Assert.Null(session.WorkspaceId);
        Assert.Equal("codex", session.Provider);
        Assert.Null(session.ProviderThreadId);
        Assert.Equal("Decision", session.Role);
        Assert.Equal(legacyGuid, session.LegacySessionGuid);
        Assert.Equal(now, session.StartedAt);
        Assert.Null(session.CompletedAt);

        await store.UpsertAgentSessionAsync(new AgentSessionRecord(
            "ses_001",
            "att_001",
            "ws_workspace",
            "codex",
            "thread-123",
            "Decision",
            legacyGuid,
            now,
            now.AddMinutes(4)));

        AgentSessionRecord updated = Assert.Single(await store.ReadAgentSessionsAsync());
        Assert.Equal("att_001", updated.AttemptId);
        Assert.Equal("ws_workspace", updated.WorkspaceId);
        Assert.Equal("thread-123", updated.ProviderThreadId);
        Assert.Equal(now.AddMinutes(4), updated.CompletedAt);
    }

    [Fact]
    public async Task Agent_turn_append_round_trips_ordered_and_duplicate_turn_index_throws()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await store.AppendAgentTurnAsync(new AgentTurnRecord("turn_second", "ses_001", 2, now.AddSeconds(20)));
        await store.AppendAgentTurnAsync(new AgentTurnRecord("turn_first", "ses_001", 1, now.AddSeconds(10)));

        IReadOnlyList<AgentTurnRecord> turns = await store.ReadAgentTurnsAsync();
        Assert.Equal(2, turns.Count);
        Assert.Equal("turn_first", turns[0].TurnId);
        Assert.Equal(1, turns[0].TurnIndex);
        Assert.Equal("ses_001", turns[0].SessionId);
        Assert.Equal(now.AddSeconds(10), turns[0].RecordedAt);
        Assert.Equal("turn_second", turns[1].TurnId);
        Assert.Equal(2, turns[1].TurnIndex);

        await Assert.ThrowsAsync<SqliteException>(() => store.AppendAgentTurnAsync(
            new AgentTurnRecord("turn_duplicate", "ses_001", 1, now.AddSeconds(30))));

        await store.AppendAgentTurnAsync(new AgentTurnRecord("turn_other_session", "ses_002", 1, now.AddSeconds(40)));
        Assert.Equal(3, (await store.ReadAgentTurnsAsync()).Count);
    }

    [Fact]
    public async Task Read_workspace_identity_creates_schema_and_stays_stable()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);

        string first = await store.ReadWorkspaceIdentityAsync();
        string second = await store.ReadWorkspaceIdentityAsync();

        Assert.StartsWith("ws_", first, StringComparison.Ordinal);
        Assert.Equal(29, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Spine_reads_return_empty_when_database_is_missing()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);

        Assert.Empty(await store.ReadRunsAsync());
        Assert.Empty(await store.ReadWorkflowInstancesAsync());
        Assert.Empty(await store.ReadAttemptsAsync());
        Assert.Empty(await store.ReadAgentSessionsAsync());
        Assert.Empty(await store.ReadAgentTurnsAsync());
    }

    [Fact]
    public async Task Spine_reads_return_empty_when_database_predates_spine_tables()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection legacy = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await legacy.OpenAsync();
            await using SqliteCommand command = legacy.CreateCommand();
            command.CommandText = """
                CREATE TABLE schema_metadata(key text primary key, value text not null);
                INSERT INTO schema_metadata (key, value) VALUES ('schema_version', '2');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var store = new CanonicalWorkflowPersistenceStore(repository);

        Assert.Empty(await store.ReadRunsAsync());
        Assert.Empty(await store.ReadWorkflowInstancesAsync());
        Assert.Empty(await store.ReadAttemptsAsync());
        Assert.Empty(await store.ReadAgentSessionsAsync());
        Assert.Empty(await store.ReadAgentTurnsAsync());
    }

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-canonical-spine-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }
}
