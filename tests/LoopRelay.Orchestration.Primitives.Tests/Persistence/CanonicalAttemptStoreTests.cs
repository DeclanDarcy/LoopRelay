using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Persistence;

public sealed class CanonicalAttemptStoreTests
{
    [Fact]
    public async Task Attempt_started_row_round_trips_through_the_store()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var store = new CanonicalAttemptStore(persistence);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await store.PersistAttemptStartedAsync(
            new AttemptRecord("att_001", "tr_001", "wfi_001", "run_001", 1, now, null, null),
            CancellationToken.None);

        AttemptRecord attempt = Assert.Single(await persistence.ReadAttemptsAsync());
        Assert.Equal("att_001", attempt.AttemptId);
        Assert.Equal("tr_001", attempt.TransitionRunId);
        Assert.Equal("wfi_001", attempt.WorkflowInstanceId);
        Assert.Equal("run_001", attempt.RunId);
        Assert.Equal(1, attempt.AttemptIndex);
        Assert.Equal(now, attempt.StartedAt);
        Assert.Null(attempt.CompletedAt);
        Assert.Null(attempt.Outcome);
        Assert.Null(attempt.PolicyId);
    }

    [Fact]
    public async Task Attempt_round_trips_its_policy_identity()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var store = new CanonicalAttemptStore(persistence);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await store.PersistAttemptStartedAsync(
            new AttemptRecord("att_001", "tr_001", "wfi_001", "run_001", 1, now, null, null, "pol_v1_0123456789abcdef0123456789abcdef"),
            CancellationToken.None);

        AttemptRecord attempt = Assert.Single(await persistence.ReadAttemptsAsync());
        Assert.Equal("pol_v1_0123456789abcdef0123456789abcdef", attempt.PolicyId);
    }

    [Fact]
    public async Task Attempt_completion_updates_only_completion_columns()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var store = new CanonicalAttemptStore(persistence);
        DateTimeOffset now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

        await store.PersistAttemptStartedAsync(
            new AttemptRecord("att_001", "tr_001", "wfi_001", "run_001", 1, now, null, null),
            CancellationToken.None);
        await store.PersistAttemptCompletedAsync(new AttemptIdentity("att_001"), now.AddMinutes(3), "Completed", CancellationToken.None);

        AttemptRecord attempt = Assert.Single(await persistence.ReadAttemptsAsync());
        Assert.Equal("att_001", attempt.AttemptId);
        Assert.Equal("tr_001", attempt.TransitionRunId);
        Assert.Equal("wfi_001", attempt.WorkflowInstanceId);
        Assert.Equal("run_001", attempt.RunId);
        Assert.Equal(1, attempt.AttemptIndex);
        Assert.Equal(now, attempt.StartedAt);
        Assert.Equal(now.AddMinutes(3), attempt.CompletedAt);
        Assert.Equal("Completed", attempt.Outcome);
    }

    [Fact]
    public async Task Completing_an_unknown_attempt_is_a_no_op()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var store = new CanonicalAttemptStore(persistence);

        await store.PersistAttemptCompletedAsync(
            new AttemptIdentity("att_missing"),
            new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero),
            "Failed",
            CancellationToken.None);

        Assert.Empty(await persistence.ReadAttemptsAsync());
    }

    [Fact]
    public async Task Workflow_instance_recorder_begins_active_instances_and_completes_them_in_place()
    {
        Repository repository = CreateRepository();
        var persistence = new CanonicalWorkflowPersistenceStore(repository);
        var recorder = new CanonicalWorkflowInstanceRecorder(persistence);

        WorkflowInstanceIdentity workflowInstance = await recorder.BeginInstanceAsync(
            new RunIdentity("run_001"), WorkflowIdentity.Plan, CancellationToken.None);

        Assert.StartsWith("wfi_", workflowInstance.Value, StringComparison.Ordinal);
        WorkflowInstanceRecord begun = Assert.Single(await persistence.ReadWorkflowInstancesAsync());
        Assert.Equal(workflowInstance.Value, begun.WorkflowInstanceId);
        Assert.Equal("run_001", begun.RunId);
        Assert.Equal(WorkflowIdentity.Plan, begun.Workflow);
        Assert.Equal(CanonicalWorkflowCatalog.Current.SemanticVersion, begun.CatalogVersion);
        Assert.Equal(CanonicalWorkflowCatalog.Current.Identity, begun.CatalogIdentity);
        Assert.Equal("Active", begun.Status);
        Assert.Null(begun.CompletedAt);
        Assert.Null(begun.Outcome);

        await recorder.CompleteInstanceAsync(workflowInstance, "Stopped", "TransitionCompleted", CancellationToken.None);

        WorkflowInstanceRecord completed = Assert.Single(await persistence.ReadWorkflowInstancesAsync());
        Assert.Equal(workflowInstance.Value, completed.WorkflowInstanceId);
        Assert.Equal("run_001", completed.RunId);
        Assert.Equal(begun.StartedAt, completed.StartedAt);
        Assert.Equal("Stopped", completed.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.Equal("TransitionCompleted", completed.Outcome);
    }

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("looprelay-canonical-attempt-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }
}
