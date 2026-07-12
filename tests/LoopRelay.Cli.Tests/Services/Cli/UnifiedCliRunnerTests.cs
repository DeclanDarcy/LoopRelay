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
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Workflows;
using Microsoft.Data.Sqlite;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class UnifiedCliRunnerTests
{
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

    [Fact]
    public async Task RunAsync_aborts_with_typed_outcome_when_a_runtime_prerequisite_is_missing()
    {
        // M7: a production run inspects the provider's runtime prerequisites before any agent
        // launches; an Error diagnostic aborts with the specific MissingRuntimePrerequisite
        // outcome (exit 4) instead of the raw resolver exception at the first send, and the
        // inspection is appended as an append-only fact.
        Repository repository = CreateRepository();
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        composition.ProductionRuntime = true;
        composition.RuntimePrerequisiteProfile = HostProfile();
        composition.RuntimePrerequisiteDoctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Contains("Stop reason: MissingRuntimePrerequisite", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("CODEX_EXECUTABLE is not set.", error.ToString(), StringComparison.Ordinal);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        CanonicalRuntimePrerequisiteRecord check = Assert.Single(await store.ReadRuntimePrerequisitesAsync());
        Assert.StartsWith("pre_", check.PrerequisiteCheckId, StringComparison.Ordinal);
        Assert.Null(check.RunId);
        Assert.Contains("runtime.codex_executable.missing", check.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Contains("MissingRequiredExecutable", check.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Contains("runtime_cli_test", check.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Contains("\"provider\":\"codex\"", check.DiagnosticsJson, StringComparison.Ordinal);
        Assert.Contains("Unsatisfied", check.DiagnosticsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_production_compositions_have_no_runtime_prerequisites_to_inspect()
    {
        // Injected runtimes have no provider prerequisites: the inspection returns nothing and
        // appends no fact, so unit-test compositions never gate on the machine's environment.
        Repository repository = CreateRepository();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        composition.RuntimePrerequisiteDoctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);

        RuntimePrerequisiteApplicationResult result =
            await composition.InspectRuntimePrerequisitesAsync(CancellationToken.None);
        Assert.Null(result.Evidence);
        Assert.Null(result.StopReason);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        Assert.Empty(await store.ReadRuntimePrerequisitesAsync());
    }

    [Fact]
    public async Task Non_runtime_commands_bypass_production_runtime_inspection()
    {
        Repository repository = CreateRepository();
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new UnifiedCliCommand(UnifiedCliCommandKind.StorageInit, []));
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        composition.ProductionRuntime = true;
        composition.RuntimePrerequisiteProfile = HostProfile();
        composition.RuntimePrerequisiteDoctor = new RuntimePrerequisiteDoctor(_ => null, _ => false);
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await new UnifiedCliRunner(composition, output, error)
            .RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("CODEX_EXECUTABLE", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("CODEX_EXECUTABLE", error.ToString(), StringComparison.Ordinal);
        Assert.Empty(await new CanonicalWorkflowPersistenceStore(repository).ReadRuntimePrerequisitesAsync());
    }

    [Fact]
    public async Task RunAsync_executes_workflow_chain_runner_for_run_commands()
    {
        Repository repository = CreateRepository();
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(1, exitCode);
        string text = output.ToString();
        Assert.Contains("Workflow: TraditionalRoadmap", text, StringComparison.Ordinal);
        Assert.Contains("Stop reason: Failed", text, StringComparison.Ordinal);
        Assert.Contains("Transition: BootstrapRoadmapCompletionContext", text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_completed_chain_returns_success_without_old_execute_loop()
    {
        Repository repository = CreateRepository();
        // Collaboration-file products are filesystem-authoritative (M3): the persisted rows
        // below only annotate them, so the actual files must exist to be observed.
        await SeedRoadmapArtifactsAsync(repository);
        await SeedPlanArtifactsAsync(repository);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        await PersistCompletedChainAsync(store);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.True(exitCode == 0, $"Output:{Environment.NewLine}{output}{Environment.NewLine}Error:{Environment.NewLine}{error}");
        Assert.Contains("Workflow: Execute", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Stop reason: ChainCompleted", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_storage_init_creates_canonical_workspace_schema()
    {
        Repository repository = CreateRepository();
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new UnifiedCliCommand(UnifiedCliCommandKind.StorageInit, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Assert.True(File.Exists(databasePath));
        Assert.Contains("Storage initialized.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains(databasePath, output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync();
        Assert.Equal(LoopRelayWorkspaceDatabase.CurrentSchemaVersion.ToString(), await ScalarAsync(
            connection,
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

        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new UnifiedCliCommand(UnifiedCliCommandKind.StorageInit, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Unsupported SQLite schema version", error.ToString(), StringComparison.Ordinal);
        await using SqliteConnection verify = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await verify.OpenAsync();
        Assert.Equal("999", await ScalarAsync(
            verify,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
    }

    [Fact]
    public async Task RunAsync_status_migrates_pre_m2_database_with_latched_blocked_row_before_any_read()
    {
        Repository repository = CreateRepository();
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using (SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath))
        {
            // A v3-shaped database is built with raw SQL only: EnsureSchemaAsync must never run here,
            // because the point is that the CLI itself migrates before its first read.
            await connection.OpenAsync();
            await ExecuteAsync(
                connection,
                "CREATE TABLE schema_metadata(key text primary key, value text not null);");
            await ExecuteAsync(
                connection,
                "INSERT INTO schema_metadata(key, value) VALUES ('schema_version', '3');");
            await ExecuteAsync(
                connection,
                """
                CREATE TABLE canonical_workflow_states(
                    workflow_identity text primary key,
                    state text not null,
                    current_stage text,
                    outcome text,
                    updated_at text not null,
                    evidence_json text not null
                );
                """);
            await ExecuteAsync(
                connection,
                """
                INSERT INTO canonical_workflow_states (workflow_identity, state, current_stage, outcome, updated_at, evidence_json)
                VALUES ('Plan', 'Blocked', 'Planning', 'Blocked', '2026-07-10T12:00:00.0000000Z', '["legacy-blocked.md"]');
                """);
        }

        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Status, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("Selected workflow: Plan", output.ToString(), StringComparison.Ordinal);
        await using SqliteConnection verify = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await verify.OpenAsync();
        Assert.Equal("Resumable", await ScalarAsync(
            verify,
            "SELECT state FROM canonical_workflow_states WHERE workflow_identity = 'Plan';"));
        Assert.Equal(LoopRelayWorkspaceDatabase.CurrentSchemaVersion.ToString(), await ScalarAsync(
            verify,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
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

        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Status, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Unsupported SQLite schema version `99`", error.ToString(), StringComparison.Ordinal);
        await using SqliteConnection verify = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await verify.OpenAsync();
        Assert.Equal("99", await ScalarAsync(
            verify,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
    }

    [Fact]
    public async Task RunAsync_storage_import_initializes_shared_workspace_schema()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new UnifiedCliCommand(UnifiedCliCommandKind.StorageImport, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Assert.True(File.Exists(databasePath));
        Assert.Contains("Storage import completed.", output.ToString(), StringComparison.Ordinal);
        Assert.Contains($"Database: {databasePath}", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        await using SqliteConnection connection = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await connection.OpenAsync();
        Assert.Equal(LoopRelayWorkspaceDatabase.CurrentSchemaVersion.ToString(), await ScalarAsync(
            connection,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
        Assert.Equal("imported", await ScalarAsync(
            connection,
            "SELECT value FROM workspace_metadata WHERE key = 'persistence_state';"));
    }

    [Fact]
    public async Task RunAsync_storage_sync_rejects_extra_arguments()
    {
        Repository repository = CreateRepository();
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new UnifiedCliCommand(UnifiedCliCommandKind.StorageSync, ["extra"]));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Unexpected argument: extra", error.ToString(), StringComparison.Ordinal);
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
                """);
        }

        // The next store write runs the schema pass, which migrates the legacy label without any unblock step.
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

        var runInvocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var runOutput = new StringWriter();
        using var runError = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), runOutput, runError);

        int runExitCode = await runner.RunAsync(runInvocation, CancellationToken.None);

        Assert.Equal(4, runExitCode);
        Assert.Contains("Stop reason: MissingRequiredInput", runOutput.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, runError.ToString());
    }

    [Fact]
    public async Task RunAsync_dirty_input_surface_stops_with_exit_4_and_prints_requirement_warnings()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        await GitAsync(repository.Path, "init");
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

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
    public async Task RunAsync_bounded_plan_stops_after_one_completed_workflow()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Plan,
            WorkflowResolutionState.Completed,
            null,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            ["plan-complete.md"]));
        await PersistPlanProductsAsync(store);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Workflow: Plan", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Stop reason: BoundedWorkflowCompleted", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_bounded_plan_verifies_existing_execution_artifacts_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        await SeedPlanArtifactsAsync(repository);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
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
    public async Task RunAsync_bounded_traditional_verifies_existing_roadmap_products_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedTraditional),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.True(exitCode == 0, $"Output:\n{output}\nError:\n{error}");
        Assert.Contains("Workflow: TraditionalRoadmap", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Stop reason: TransitionCompleted", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyPlanEntryContract", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.TraditionalRoadmap);
        Assert.Equal(WorkflowResolutionState.Completed, state.State);
    }

    [Fact]
    public async Task RunAsync_bounded_eval_verifies_existing_eval_roadmap_products_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        await SeedEvalIntentAsync(repository);
        await SeedRoadmapArtifactsAsync(repository);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedEval),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Workflow: EvalRoadmap", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Stop reason: TransitionCompleted", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyPlanEntryContract", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(WorkflowResolutionState.Completed, state.State);
    }

    [Fact]
    public async Task RunAsync_bounded_eval_selects_evaluation_intent_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        await SeedEvalIntentAsync(repository);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedEval),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
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
        string evidencePath = Path.Combine(
            repository.Path,
            ".LoopRelay",
            "evidence",
            "local-verification",
            "SelectEvaluationIntent.md");
        Assert.True(File.Exists(evidencePath));
        Assert.Contains("Transition: SelectEvaluationIntent", await File.ReadAllTextAsync(evidencePath), StringComparison.Ordinal);
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
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedExecute),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
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
    public async Task RunAsync_bounded_execute_verifies_workflow_exit_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        // CompletionEvidence is a collaboration file (M3): the persisted row below only
        // annotates it, so the actual evidence file must exist to be observed.
        await WriteAsync(
            repository.Path,
            ".agents/evidence/evaluations/completion-certification.md",
            "# Completion Certification");
        var store = new CanonicalWorkflowPersistenceStore(repository);
        await store.UpsertProductAsync(Product(
            ProductIdentity.CompletionEvidence,
            WorkflowIdentity.Execute,
            WorkflowIdentity.Execute));
        await store.UpsertProductAsync(Product(
            ProductIdentity.CompletionRoute,
            WorkflowIdentity.Execute,
            WorkflowIdentity.Execute));
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Execute,
            WorkflowResolutionState.Resumable,
            new WorkflowStageIdentity("Workflow Completion"),
            RuntimeOutcomeKind.Waiting,
            DateTimeOffset.UtcNow,
            ["execute-workflow-completion-test"]));
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedExecute),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        string text = output.ToString();
        Assert.Contains("Workflow: Execute", text, StringComparison.Ordinal);
        Assert.Contains("Stop reason: TransitionCompleted", text, StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyWorkflowExitGate", text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.Execute);
        Assert.Equal(WorkflowResolutionState.Completed, state.State);
        Assert.Null(state.CurrentStage);
        Assert.Contains(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.CertifiedCompletion &&
                product.Product.ProducerTransition == new WorkflowTransitionIdentity("VerifyWorkflowExitGate") &&
                product.Product.ValidationState == ProductValidationState.Valid &&
                product.GateUsable);
        string evidencePath = Path.Combine(
            repository.Path,
            ".LoopRelay",
            "evidence",
            "local-verification",
            "VerifyWorkflowExitGate.md");
        Assert.True(File.Exists(evidencePath));
        Assert.Contains("Transition: VerifyWorkflowExitGate", await File.ReadAllTextAsync(evidencePath), StringComparison.Ordinal);
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();
        CanonicalTransitionRunRecord run = Assert.Single(
            snapshot.TransitionRuns,
            item => item.Transition == new WorkflowTransitionIdentity("VerifyWorkflowExitGate"));
        CanonicalEffectRecord effect = Assert.Single(
            snapshot.EffectRecords,
            item => item.Effect == new EffectIdentity("record-certified-completion"));
        Assert.Equal(run.RunId, effect.RunId);
        Assert.Equal(EffectCategory.Archive, effect.Category);
        Assert.Equal(EffectExecutionStatus.Succeeded, effect.Status);
    }

    [Fact]
    public async Task RunAsync_default_chained_without_eval_intent_continues_through_plan_and_execute()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        await SeedPlanArtifactsAsync(repository);
        await SeedCompletionArchiveAsync(repository);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.True(exitCode == 0, $"Output:\n{output}\nError:\n{error}");
        string text = output.ToString();
        Assert.Contains("Workflow: TraditionalRoadmap", text, StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyPlanEntryContract", text, StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyExecuteEntryContract", text, StringComparison.Ordinal);
        Assert.Contains("Workflow: Execute", text, StringComparison.Ordinal);
        Assert.Contains("Stop reason: ChainCompleted", text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_unbounded_traditional_reobserves_completed_transitions_and_continues_through_execute()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        await SeedPlanArtifactsAsync(repository);
        await SeedCompletionArchiveAsync(repository);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.True(exitCode == 0, $"Output:\n{output}\nError:\n{error}");
        string text = output.ToString();
        Assert.Contains("Transition: VerifyPlanEntryContract", text, StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyExecuteEntryContract", text, StringComparison.Ordinal);
        Assert.Contains("Workflow: Execute", text, StringComparison.Ordinal);
        Assert.Contains("Stop reason: ChainCompleted", text, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(text, "Stop reason: TransitionCompleted"));
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        Assert.Contains(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.TraditionalRoadmap &&
                workflow.State == WorkflowResolutionState.Completed);
        Assert.Contains(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.Plan &&
                workflow.State == WorkflowResolutionState.Completed);
        Assert.Contains(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.Execute &&
                workflow.State == WorkflowResolutionState.Completed);
    }

    [Theory]
    [InlineData(InvocationModeKind.DefaultChained)]
    [InlineData(InvocationModeKind.ForcedEvalChain)]
    public async Task RunAsync_eval_chained_invocations_continue_through_plan_and_execute(
        InvocationModeKind mode)
    {
        Repository repository = CreateRepository();
        await SeedEvalIntentAsync(repository);
        await SeedRoadmapArtifactsAsync(repository);
        await SeedPlanArtifactsAsync(repository);
        await SeedCompletionArchiveAsync(repository);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(mode),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        string text = output.ToString();
        Assert.Contains("Workflow: EvalRoadmap", text, StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyPlanEntryContract", text, StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyExecuteEntryContract", text, StringComparison.Ordinal);
        Assert.Contains("Workflow: Execute", text, StringComparison.Ordinal);
        Assert.Contains("Stop reason: ChainCompleted", text, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(text, "Stop reason: TransitionCompleted"));
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        Assert.Contains(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.EvalRoadmap &&
                workflow.State == WorkflowResolutionState.Completed);
        Assert.Contains(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.Plan &&
                workflow.State == WorkflowResolutionState.Completed);
        Assert.Contains(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.Execute &&
                workflow.State == WorkflowResolutionState.Completed);
    }

    [Fact]
    public async Task RunAsync_run_command_writes_terminal_run_row_and_interrupts_lingering_active_runs()
    {
        Repository repository = CreateRepository();
        var store = new CanonicalWorkflowPersistenceStore(repository);
        await store.UpsertRunAsync(new RunRecord(
            "run_lingering",
            "ws_lingering",
            "BoundedPlan",
            "BoundedPlan",
            "Active",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            null,
            null,
            string.Empty));
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Plan,
            WorkflowResolutionState.Completed,
            null,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            ["plan-complete.md"]));
        await PersistPlanProductsAsync(store);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        IReadOnlyList<RunRecord> runs = await store.ReadRunsAsync();
        Assert.Equal(2, runs.Count);
        RunRecord lingering = Assert.Single(runs, run => run.RunId == "run_lingering");
        Assert.Equal("Interrupted", lingering.Status);
        Assert.NotNull(lingering.CompletedAt);
        RunRecord current = Assert.Single(runs, run => run.RunId != "run_lingering");
        Assert.StartsWith("run_", current.RunId, StringComparison.Ordinal);
        Assert.StartsWith("ws_", current.WorkspaceId, StringComparison.Ordinal);
        Assert.Equal("BoundedPlan", current.ChainIdentity);
        Assert.Equal(InvocationModeKind.BoundedPlan.ToString(), current.InvocationMode);
        Assert.Equal(WorkflowStopReason.BoundedWorkflowCompleted.ToString(), current.Status);
        Assert.Equal(WorkflowStopReason.BoundedWorkflowCompleted.ToString(), current.StopReason);
        Assert.NotNull(current.CompletedAt);
    }

    [Fact]
    public async Task RunAsync_run_command_records_cancelled_run_row_on_cancellation()
    {
        Repository repository = CreateRepository();
        await SeedRoadmapArtifactsAsync(repository);
        var invocation = new UnifiedCliInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.ForcedTraditionalChain),
            new UnifiedCliCommand(UnifiedCliCommandKind.Run, []));
        using var source = new CancellationTokenSource();
        using var output = new CancelOnStopReasonWriter(source);
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(UnifiedCliComposition.Create(repository), output, error);

        int? exitCode = null;
        try
        {
            exitCode = await runner.RunAsync(invocation, source.Token);
        }
        catch (OperationCanceledException)
        {
            // Cancellation may surface as a thrown OperationCanceledException or as a Cancelled stop reason.
        }

        var store = new CanonicalWorkflowPersistenceStore(repository);
        IReadOnlyList<RunRecord> runs = await store.ReadRunsAsync();
        RunRecord run = Assert.Single(runs);
        Assert.Equal(WorkflowStopReason.Cancelled.ToString(), run.Status);
        Assert.Equal(WorkflowStopReason.Cancelled.ToString(), run.StopReason);
        Assert.NotNull(run.CompletedAt);
        if (exitCode is not null)
        {
            Assert.Equal(130, exitCode);
        }
    }

    private sealed class CancelOnStopReasonWriter(CancellationTokenSource _source) : StringWriter
    {
        public override void WriteLine(string? value)
        {
            base.WriteLine(value);
            if (value is not null && value.StartsWith("Stop reason:", StringComparison.Ordinal))
            {
                _source.Cancel();
            }
        }
    }

    private static ResolvedRuntimeHostProfile HostProfile() =>
        new(
            new RuntimeProfileIdentity("runtime_cli_test"),
            new AgentRuntimeCapabilities("codex", true, true, true));

    private static Repository CreateRepository()
    {
        string path = Directory.CreateTempSubdirectory("cc-cli-unified-runner-").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path,
        };
    }

    private static async Task GitAsync(string repositoryPath, params string[] arguments)
    {
        ProcessRunResult result = await new ProcessRunner().RunAsync("git", arguments, repositoryPath);
        Assert.Equal(0, result.ExitCode);
    }

    private static async Task PersistCompletedChainAsync(CanonicalWorkflowPersistenceStore store)
    {
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.TraditionalRoadmap,
            WorkflowResolutionState.Completed,
            null,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            ["traditional-complete.md"]));
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Plan,
            WorkflowResolutionState.Completed,
            null,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            ["plan-complete.md"]));
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Execute,
            WorkflowResolutionState.Completed,
            null,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            ["execute-complete.md"]));
        await store.UpsertProductAsync(Product(
            ProductIdentity.PreparedEpic,
            WorkflowIdentity.TraditionalRoadmap,
            WorkflowIdentity.Plan));
        await store.UpsertProductAsync(Product(
            ProductIdentity.MilestoneSpecificationSet,
            WorkflowIdentity.TraditionalRoadmap,
            WorkflowIdentity.Plan));
        await PersistPlanProductsAsync(store);
        await store.UpsertProductAsync(Product(
            ProductIdentity.CertifiedCompletion,
            WorkflowIdentity.Execute,
            WorkflowIdentity.Execute));
    }

    private static async Task PersistPlanProductsAsync(CanonicalWorkflowPersistenceStore store)
    {
        await store.UpsertProductAsync(Product(
            ProductIdentity.ExecutablePlan,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
        await store.UpsertProductAsync(Product(
            ProductIdentity.OperationalContext,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
        await store.UpsertProductAsync(Product(
            ProductIdentity.ExecutionDetails,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
        await store.UpsertProductAsync(Product(
            ProductIdentity.ExecutionMilestoneSet,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
        await store.UpsertProductAsync(Product(
            ProductIdentity.ExecutionReadiness,
            WorkflowIdentity.Plan,
            WorkflowIdentity.Execute));
    }

    private static ProductRecord Product(
        ProductIdentity identity,
        WorkflowIdentity producer,
        WorkflowIdentity consumer) =>
        new(
            identity,
            producer,
            new WorkflowTransitionIdentity($"Produce{identity}"),
            [consumer],
            "repository-owned test evidence",
            "test",
            [$"{identity}.md"],
            $"hash-{identity}",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$"{identity}.md"]);

    private static async Task WriteAsync(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static async Task SeedEvalIntentAsync(Repository repository) =>
        await WriteAsync(repository.Path, ".agents/evals/e1.md", "# Eval");

    private static async Task SeedRoadmapArtifactsAsync(Repository repository)
    {
        foreach (string path in ProjectContextSourceContract.SourceFiles)
        {
            await WriteAsync(repository.Path, path, $"# {Path.GetFileNameWithoutExtension(path)}\n\nCanonical test project context.");
        }
        await WriteAsync(repository.Path, ".agents/epic.md", "# Epic");
        await WriteAsync(repository.Path, ".agents/specs/s1.md", "# Spec");
    }

    private static async Task SeedPlanArtifactsAsync(Repository repository)
    {
        await WriteAsync(repository.Path, ".agents/plan.md", "# Plan");
        await WriteAsync(repository.Path, ".agents/operational_context.md", "# Operational Context");
        await WriteAsync(repository.Path, ".agents/details.md", "# Details");
        await WriteAsync(repository.Path, ".agents/milestones/m1.md", "# Milestone\n\n- [ ] Implement capability.");
    }

    private static async Task SeedCompletionArchiveAsync(Repository repository)
    {
        await WriteAsync(repository.Path, ".agents/archive/epics/1.md", "# Completed Epic\n\nSynthesized closure.");
        await WriteAsync(repository.Path, ".agents/archive/epics/1/archive-metadata.json", """{"SchemaVersion":"completed-epic-archive.v1"}""");
        await WriteAsync(repository.Path, ".agents/archive/epics/1/plan.md", "# Archived Plan");
        await WriteAsync(repository.Path, ".agents/archive/epics/1/milestones/m1.md", "# Archived Milestone");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarAsync(SqliteConnection connection, string commandText)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? value = await command.ExecuteScalarAsync();
        return value?.ToString();
    }
}
