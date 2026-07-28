using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Services.Process;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Core.Services.ProjectContext;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Infrastructure.Models.Diagnostics;
using LoopRelay.Infrastructure.Services.Diagnostics;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Interactions;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Storage;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

// See the grouping comment block in UnifiedCliRunnerTestBase.cs for the full rationale and
// measured-second membership of all three sibling classes. This class: 17.1s / 13 methods / 26 cases.
public sealed class UnifiedCliRunnerBoundedEvalSelectionAndPlanTests : UnifiedCliRunnerTestBase
{
    [Fact]
    public async Task RunAsync_bounded_eval_selects_evaluation_intent_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        await SeedEvalIntentAsync(repository);
        await SeedProjectContextAsync(repository);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repository.Path);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedEval),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        string text = output.ToString();
        Assert.Contains("Workflow: EvalRoadmap", text, StringComparison.Ordinal);
        Assert.Contains("Stop reason: TransitionCompleted", text, StringComparison.Ordinal);
        Assert.Contains("Transition: SelectEvaluationIntent", text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Dependency Inventory"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Evaluation Foundation"), state.CompletedStages);
        // EvaluationIntent is a collaboration file (M3): the observation reports the
        // filesystem-authoritative record, while the canonical ledger row proves the
        // runtime persisted the product through SelectEvaluationIntent.
        Assert.Contains(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.EvaluationIntent &&
                product.Product.ProducerWorkflow == WorkflowIdentity.EvalRoadmap &&
                product.GateUsable);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(
            snapshot.Products,
            product => product.Identity == ProductIdentity.EvaluationIntent &&
                product.ProducerWorkflow == WorkflowIdentity.EvalRoadmap &&
                product.ProducerTransition == new WorkflowTransitionIdentity("SelectEvaluationIntent") &&
                product.ValidationState == ProductValidationState.Valid);
    }

    [Fact]
    public async Task RunAsync_bounded_plan_verifies_existing_execution_artifacts_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        await SeedPlanArtifactsAsync(repository);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repository.Path);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.True(exitCode == 0, $"Output:{Environment.NewLine}{output}{Environment.NewLine}Error:{Environment.NewLine}{error}");
        Assert.Contains("Workflow: Plan", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Stop reason: TransitionCompleted", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyExecuteEntryContract", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        Assert.Contains(
            observation.Products,
                product => product.Product.Identity == ProductIdentity.ExecutionReadiness &&
                product.Product.ValidationState == ProductValidationState.Valid);
    }

    [Fact]
    public async Task RunAsync_bounded_execute_verifies_existing_execution_readiness_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        // Plan collaboration files are filesystem-authoritative (M3): the persisted rows
        // below only annotate them, so the actual files must exist to be observed.
        await SeedPlanArtifactsAsync(repository);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        await PersistPlanProductsAsync(store);
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Execute,
            WorkflowResolutionState.Resumable,
            new WorkflowStageIdentity("Execution Readiness"),
            RuntimeOutcomeKind.Waiting,
            DateTimeOffset.UtcNow,
            ["execute-readiness-test"]));
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedExecute),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Workflow: Execute", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Stop reason: TransitionCompleted", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyExecutionReadiness", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.Execute);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Implementation Planning"), state.CurrentStage);
    }

    [Fact]
    public async Task RunAsync_dirty_input_surface_stops_with_exit_4_and_prints_requirement_warnings()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        await GitAsync(repository.Path, "init");
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        string text = output.ToString();
        Assert.Contains("Stop reason: DirtyInputSurface", text, StringComparison.Ordinal);
        Assert.Contains("Outcome: DirtyInputSurface", text, StringComparison.Ordinal);
        Assert.Contains("Durable state: InputUnsatisfied", text, StringComparison.Ordinal);
        Assert.Contains("Warning: WriteExecutablePlan.CleanInput:", text, StringComparison.Ordinal);
        Assert.Contains("commit the listed files under '.agents/'", text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_run_command_reenters_and_terminally_completes_matching_active_root()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        string workspace = await store.ReadWorkspaceIdentityAsync();
        await store.UpsertRunAsync(new RunRecord(
            "run_lingering",
            workspace,
            "BoundedPlan",
            "BoundedPlan",
            "Active",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            null,
            string.Empty,
            CanonicalWorkflowCatalog.Current.Identity,
            CanonicalWorkflowCatalog.Current.SemanticVersion));
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Plan,
            WorkflowResolutionState.Completed,
            null,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            ["plan-complete.md"]));
        await PersistPlanProductsAsync(store);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        IReadOnlyList<RunRecord> runs = await store.ReadRunsAsync();
        RunRecord current = Assert.Single(runs);
        Assert.Equal("run_lingering", current.RunId);
        Assert.StartsWith("ws_", current.WorkspaceId, StringComparison.Ordinal);
        Assert.Equal("BoundedPlan", current.ChainIdentity);
        Assert.Equal(InvocationModeKind.BoundedPlan.ToString(), current.InvocationMode);
        Assert.Equal(WorkflowStopReason.BoundedWorkflowCompleted.ToString(), current.Status);
        Assert.Equal(WorkflowStopReason.BoundedWorkflowCompleted.ToString(), current.StopReason);
        Assert.NotNull(current.CompletedAt);
    }

    [Fact]
    public async Task RunAsync_storage_migrate_is_the_only_command_that_upgrades_a_recognized_schema()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection,
                "CREATE TABLE schema_metadata(key text primary key, value text not null); INSERT INTO schema_metadata VALUES ('schema_version','8');");
        }
        byte[] beforeStatus = await File.ReadAllBytesAsync(databasePath);
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        using var statusOutput = new StringWriter();
        int statusExit = await new UnifiedCliRunner(composition, statusOutput, new StringWriter()).RunAsync(
            new TestApplicationInvocation(repository, new WorkflowInvocation(InvocationModeKind.DefaultChained),
                new TestApplicationCommand(TestApplicationCommandKind.Status, [])), CancellationToken.None);
        Assert.Equal(4, statusExit);
        Assert.Equal(beforeStatus, await File.ReadAllBytesAsync(databasePath));

        using var migrateOutput = new StringWriter();
        int migrateExit = await new UnifiedCliRunner(composition, migrateOutput, new StringWriter()).RunAsync(
            new TestApplicationInvocation(repository, new WorkflowInvocation(InvocationModeKind.DefaultChained),
                new TestApplicationCommand(TestApplicationCommandKind.StorageMigrate, [])), CancellationToken.None);

        Assert.Equal(0, migrateExit);
        Assert.Contains("Lifecycle: Completed", migrateOutput.ToString(), StringComparison.Ordinal);
        await using SqliteConnection verify = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await verify.OpenAsync();
        Assert.Equal(LoopRelayWorkspaceDatabase.CurrentSchemaVersion.ToString(), await ScalarAsync(
            verify, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
    }

    [Fact]
    public async Task RunAsync_previously_latched_blocked_workflow_runs_without_any_unblock_step()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        var stage = new WorkflowStageIdentity("Planning");
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
            await ExecuteAsync(
                connection,
                """
                INSERT INTO canonical_workflow_states (workflow_identity, state, current_stage, outcome, updated_at, evidence_json)
                VALUES ('Plan', 'Blocked', 'Planning', 'Blocked', '2026-07-10T12:00:00.0000000Z', '["legacy-blocked.md"]');

                DELETE FROM schema_metadata WHERE key = 'blocked_vocabulary_repaired';
                """);
        }

        // The next store write runs the schema pass, which migrates the legacy label without any
        // unblock step. LoopRelayWorkspaceDatabase now receipts a clean blocked-vocabulary scan so
        // it isn't repeated on every admission; the DELETE above is what a real reintroduction path
        // (legacy-import completion) does to force this next admission to re-scan and re-repair,
        // exactly like this out-of-band insert does here.
        await store.UpsertStageStateAsync(new CanonicalStageStateRecord(
            WorkflowIdentity.Plan,
            stage,
            WorkflowResolutionState.Active,
            DateTimeOffset.UtcNow,
            ["plan-stage.md"]));

        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();
        CanonicalWorkflowStateRecord workflow = Assert.Single(snapshot.WorkflowStates);
        Assert.Equal(WorkflowResolutionState.Resumable, workflow.State);
        Assert.Equal(RuntimeOutcomeKind.MissingRequiredInput, workflow.Outcome);
        Assert.Equal(stage, workflow.CurrentStage);

        var runInvocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var runOutput = new StringWriter();
        using var runError = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), runOutput, runError);

        int runExitCode = await runner.RunAsync(runInvocation, CancellationToken.None);

        Assert.Equal(4, runExitCode);
        Assert.Contains("Stop reason: MissingRequiredInput", runOutput.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, runError.ToString());
    }

    [Fact]
    public async Task RunAsync_requires_recovery_when_active_run_catalog_is_unavailable()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        string workspace = await store.ReadWorkspaceIdentityAsync();
        await store.UpsertRunAsync(new RunRecord(
            "run_old_catalog", workspace, "BoundedPlan", "BoundedPlan", "Active",
            DateTimeOffset.UtcNow.AddMinutes(-5), null, null, "", "catalog_missing", "12.0.0"));
        var invocation = new TestApplicationInvocation(repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error)
            .RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Contains("Stop reason: RecoveryRequired", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Exact catalog 'catalog_missing'", output.ToString(), StringComparison.Ordinal);
        Assert.Equal("Active", Assert.Single(await store.ReadRunsAsync()).Status);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_corrupt_workspace_database_fails_closed_without_crashing_or_leaking_paths()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await File.WriteAllTextAsync(databasePath, "not-a-sqlite-database");
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Contains("Storage authority: Corrupt", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("SQLite authority is unreadable", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(repository.Path, output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal("not-a-sqlite-database", await File.ReadAllTextAsync(databasePath));
    }

    [Fact]
    public async Task Non_production_compositions_have_no_runtime_prerequisites_to_inspect()
    {
        // Injected runtimes have no provider prerequisites: the inspection returns nothing and
        // appends no fact, so unit-test compositions never gate on the machine's environment.
        Repository repository = CreateRepository();
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        composition.RuntimePrerequisiteDoctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);

        RuntimePrerequisiteApplicationResult result =
            await composition.InspectRuntimePrerequisitesAsync(CancellationToken.None);
        Assert.Null(result.Evidence);
        Assert.Null(result.StopReason);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        Assert.Empty(await store.ReadRuntimePrerequisitesAsync());
    }

    [Fact]
    public async Task RunAsync_status_stops_with_exit_4_for_future_schema_version_without_crashing()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await ExecuteAsync(
                connection,
                "CREATE TABLE schema_metadata(key text primary key, value text not null);");
            await ExecuteAsync(
                connection,
                "INSERT INTO schema_metadata(key, value) VALUES ('schema_version', '99');");
        }

        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new TestApplicationCommand(TestApplicationCommandKind.Status, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Contains("Storage authority: Unsupported", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("99", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        await using SqliteConnection verify = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await verify.OpenAsync();
        Assert.Equal("99", await ScalarAsync(
            verify,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
    }

    [Fact]
    public async Task RunAsync_storage_init_blocks_unsupported_existing_schema_without_repairing()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            await connection.OpenAsync();
            await ExecuteAsync(
                connection,
                "CREATE TABLE schema_metadata(key text primary key, value text not null);");
            await ExecuteAsync(
                connection,
                "INSERT INTO schema_metadata(key, value) VALUES ('schema_version', '999');");
        }

        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new TestApplicationCommand(TestApplicationCommandKind.StorageInit, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Contains("Lifecycle: Refused", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Storage health: Unsupported", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("999", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        await using SqliteConnection verify = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await verify.OpenAsync();
        Assert.Equal("999", await ScalarAsync(
            verify,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
    }

    [Theory]
    [InlineData(WorkflowStopReason.ChainCompleted, 0)]
    [InlineData(WorkflowStopReason.BoundedWorkflowCompleted, 0)]
    [InlineData(WorkflowStopReason.Waiting, 0)]
    [InlineData(WorkflowStopReason.TransitionCompleted, 0)]
    [InlineData(WorkflowStopReason.Failed, 1)]
    [InlineData(WorkflowStopReason.Stalled, 3)]
    [InlineData(WorkflowStopReason.MissingRequiredInput, 4)]
    [InlineData(WorkflowStopReason.DirtyInputSurface, 4)]
    [InlineData(WorkflowStopReason.UnversionedInputSurface, 4)]
    [InlineData(WorkflowStopReason.StorageUnusable, 4)]
    [InlineData(WorkflowStopReason.MissingRuntimePrerequisite, 4)]
    [InlineData(WorkflowStopReason.Ambiguous, 4)]
    [InlineData(WorkflowStopReason.NoEligibleTransition, 4)]
    [InlineData(WorkflowStopReason.Cancelled, 130)]
    public void ExitCodeFor_maps_canonical_stop_reasons(
        WorkflowStopReason stopReason,
        int expected)
    {
        Assert.Equal(expected, UnifiedCliRunner.ExitCodeFor(stopReason));
    }
}
