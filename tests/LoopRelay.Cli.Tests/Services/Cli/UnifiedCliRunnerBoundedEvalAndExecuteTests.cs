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
// measured-second membership of all three sibling classes. This class: 17.2s / 5 methods / 5 cases.
public sealed class UnifiedCliRunnerBoundedEvalAndExecuteTests : UnifiedCliRunnerTestBase
{
    [Fact]
    public async Task RunAsync_bounded_eval_verifies_existing_eval_roadmap_products_through_canonical_runtime()
    {
        Repository repository = CreateRepository();
        await SeedEvalIntentAsync(repository);
        await SeedRoadmapArtifactsAsync(repository);
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
    public async Task RunAsync_bounded_execute_rejects_workflow_exit_without_completion_authority_candidate()
    {
        Repository repository = CreateRepository();
        // CompletionEvidence is a collaboration file (M3): the persisted row below only
        // annotates it, so the actual evidence file must exist to be observed.
        await WriteAsync(
            repository.Path,
            ".agents/evidence/evaluations/completion-certification.md",
            "# Completion Certification");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repository.Path);
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
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedExecute),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        var runner = new UnifiedCliRunner(composition, output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        string text = output.ToString();
        Assert.Contains("Workflow: Execute", text, StringComparison.Ordinal);
        Assert.Contains("Stop reason: MissingRequiredInput", text, StringComparison.Ordinal);
        Assert.Contains("Transition: VerifyWorkflowExitGate", text, StringComparison.Ordinal);
        Assert.Contains("Completion Authority has no certified candidate", text, StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            workflow => workflow.Workflow == WorkflowIdentity.Execute);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), state.CurrentStage);
        Assert.DoesNotContain(observation.Products,
            product => product.Product.Identity == ProductIdentity.CertifiedCompletion);
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();
        Assert.DoesNotContain(snapshot.TransitionRuns,
            item => item.Transition == new WorkflowTransitionIdentity("VerifyWorkflowExitGate") &&
                item.State == TransitionDurableState.Completed);
        Assert.DoesNotContain(snapshot.EffectRecords,
            item => item.Effect == new EffectIdentity("record-certified-completion"));
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
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("Workflow: Plan", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Stop reason: BoundedWorkflowCompleted", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Interaction_respond_acceptance_resolves_request_and_plans_scoped_commit_effect()
    {
        Repository repository = CreateRepository();
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository);
        var causality = new CanonicalCausalContext(
            WorkspaceIdentity.New(), RunIdentity.New(), WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(), AttemptIdentity.New());
        InteractionCategoryPolicy policy = InteractionCategoryPolicyRegistry.Resolve(
            InteractionCategory.DirtyInputCommitOffer, composition.Policy.PolicyId);
        var request = new InteractionRequest(
            InteractionRequestIdentity.New(), InteractionCategory.DirtyInputCommitOffer,
            new InteractionCausalSubject(causality, "declared-input-surface", ".agents/specs"),
            "Commit the declared surface?",
            """{"surface":".agents/specs","changedPaths":[".agents/specs/epic.md"]}""",
            policy, [".agents/specs/epic.md"], "dirty-cli-test", DateTimeOffset.UtcNow);
        InteractionAggregate aggregate = await composition.InteractionBroker.CreateAsync(new(request));
        aggregate = await composition.InteractionBroker.PresentAsync(request.Identity, aggregate.RowVersion);
        using var statusOutput = new StringWriter();
        using var statusError = new StringWriter();
        int statusExit = await new UnifiedCliRunner(composition, statusOutput, statusError).RunAsync(
            new TestApplicationInvocation(repository, new WorkflowInvocation(InvocationModeKind.DefaultChained),
                new TestApplicationCommand(TestApplicationCommandKind.Status, [])),
            CancellationToken.None);
        Assert.Equal(0, statusExit);
        Assert.Contains(request.Identity.Value, statusOutput.ToString(), StringComparison.Ordinal);
        Assert.Contains("Commit the declared surface?", statusOutput.ToString(), StringComparison.Ordinal);
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            new TestApplicationCommand(TestApplicationCommandKind.InteractionRespond,
                [request.Identity.Value, """{"accept":true}"""]));
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await new UnifiedCliRunner(composition, output, error)
            .RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
        InteractionAggregate resolved = await composition.InteractionBroker.ShowAsync(new(request.Identity));
        Assert.True(resolved.ResumeAuthorized);
        EffectWorkItem effect = Assert.Single(await new CanonicalEffectWorkStore(repository)
            .ReadPlanAsync(causality.TransitionRun, CancellationToken.None));
        GitEffectPayload payload = System.Text.Json.JsonSerializer.Deserialize<GitEffectPayload>(
            effect.Intent.TypedPayload,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Equal(".agents/specs", payload.Pathspec);
        Assert.Contains($"effect:{effect.Intent.Identity.Value}",
            resolved.Events.Single(item => item.Lifecycle == InteractionLifecycle.Resolved).Evidence);
    }

    [Fact]
    public async Task RunAsync_status_reports_migration_required_without_mutating_pre_m2_database()
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

        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            new TestApplicationCommand(TestApplicationCommandKind.Status, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(4, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("Selected workflow: Plan", output.ToString(), StringComparison.Ordinal);
        await using SqliteConnection verify = LoopRelayWorkspaceDatabase.OpenReadOnly(databasePath);
        await verify.OpenAsync();
        Assert.Equal("Blocked", await ScalarAsync(
            verify,
            "SELECT state FROM canonical_workflow_states WHERE workflow_identity = 'Plan';"));
        Assert.Equal("3", await ScalarAsync(
            verify,
            "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
    }
}
