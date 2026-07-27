using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Application.Contracts;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Planning;
using LoopRelay.Cli.Tests.Models;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Completion.Models.Authority;
using LoopRelay.Completion.Services.Authority;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Policy;
using LoopRelay.Orchestration.Recovery;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Configuration;
using LoopRelay.Permissions.Services.Evaluation;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class CompositionRootTraditionalRoadmapAndSpineTests : CompositionRootTestBase
{
    [Fact]
    public async Task TraditionalRoadmap_accepts_inline_code_file_markers_from_provider_output()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-traditional-full-runtime").FullName;
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        EnqueueTraditionalOutput(runtime, "BootstrapRoadmapCompletionContext", "# Roadmap Completion Context");
        EnqueueTraditionalOutput(runtime, "SelectStrategicInitiative", "# Strategic Initiative Selection");
        EnqueueTraditionalOutput(runtime, "CreateNewEpic", ValidEvalActiveEpic());
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("Generate Milestone Deep Dives For Epic", prompt, StringComparison.Ordinal);
            Assert.Contains("## Active Epic", prompt, StringComparison.Ordinal);
            Assert.Contains("Source: .agents/epic.md", prompt, StringComparison.Ordinal);
            Assert.Contains("including the leading dot", prompt, StringComparison.Ordinal);
            Assert.Contains("never `agents/epic.md`", prompt, StringComparison.Ordinal);
            Assert.Contains("# Epic:", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(3, AgentTurnState.Completed, """
                # Milestone Deep Dive Bundle

                # FILE: `.agents/specs/m1.md`

                # Milestone Spec: Implement the roadmap capability

                ## Purpose

                Implement the roadmap capability.

                # FILE: `.agents/specs/m2.md`

                # Milestone Spec: Verify the roadmap capability

                ## Purpose

                Verify the roadmap capability.

                # FILE: `.agents/specs/m3.md`

                # Milestone Spec: Harden the roadmap capability

                ## Purpose

                Harden the roadmap capability.
                """, AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(repository, runtime);

        TransitionRuntimeResult context = await RunTraditionalAsync(
            composition,
            "Roadmap Context",
            "BootstrapRoadmapCompletionContext");
        TransitionRuntimeResult selection = await RunTraditionalAsync(
            composition,
            "Strategic Initiative Selection",
            "SelectStrategicInitiative");
        TransitionRuntimeResult epic = await RunTraditionalAsync(
            composition,
            "Epic Preparation",
            "CreateEpic");
        TransitionRuntimeResult specs = await RunTraditionalAsync(
            composition,
            "Milestone Specification",
            "GenerateMilestoneDeepDivesForEpic");
        TransitionRuntimeResult verify = await RunTraditionalAsync(
            composition,
            "Workflow Completion",
            "VerifyPlanEntryContract");

        Assert.All(
            [context, selection, epic, specs, verify],
            result => Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation));
        Assert.Equal(4, runtime.OneShotCalls.Count);
        Assert.Equal(ValidEvalActiveEpic(), await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "epic.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.WorkflowStates, state =>
            state.Workflow == WorkflowIdentity.TraditionalRoadmap &&
            state.State == WorkflowResolutionState.Completed &&
            state.CurrentStage is null);
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.PreparedEpic &&
            product.StorageRepresentations.Contains(".agents/epic.md"));
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.MilestoneSpecificationSet &&
            product.StorageRepresentations.Contains(".agents/specs/m1.md") &&
            product.StorageRepresentations.Contains(".agents/specs/m2.md") &&
            product.StorageRepresentations.Contains(".agents/specs/m3.md"));
        string effectEvidencePath = Path.Combine(
            repo,
            ".LoopRelay",
            "evidence",
            "traditional-roadmap-effects",
            "CreateEpic.md");
        Assert.True(File.Exists(effectEvidencePath));
        string effectEvidence = await File.ReadAllTextAsync(effectEvidencePath);
        Assert.Contains("Transition Ordering", effectEvidence, StringComparison.Ordinal);
        Assert.Contains("Prompt Execution Sequencing", effectEvidence, StringComparison.Ordinal);
        Assert.Contains("Selection Provenance", effectEvidence, StringComparison.Ordinal);
        Assert.Contains("Recovery Intent", effectEvidence, StringComparison.Ordinal);
        await AssertEffectStateAsync(repository, "persist-prepared-epic", EffectLifecycle.Succeeded);
        IReadOnlyList<EffectWorkItem> publicationPlan = await new CanonicalEffectWorkStore(repository)
            .ReadPlanAsync(epic.TransitionRun!.Value, CancellationToken.None);
        Assert.Contains(publicationPlan, item => item.Intent.Executor == GitEffectExecutorKeys.NestedRepositoryCommit &&
            item.Intent.Requiredness == EffectRequiredness.BlockingLocal);
        Assert.Contains(publicationPlan, item => item.Intent.Executor == GitEffectExecutorKeys.NestedRepositoryPush &&
            item.Intent.Requiredness == EffectRequiredness.RequiredAsync);
        Assert.Contains(publicationPlan, item => item.Intent.Executor == GitEffectExecutorKeys.ParentGitlinkCommit &&
            item.Intent.Requiredness == EffectRequiredness.BlockingLocal);
        Assert.Contains(publicationPlan, item => item.Intent.Executor == GitEffectExecutorKeys.ParentRepositoryPush &&
            item.Intent.Requiredness == EffectRequiredness.RequiredAsync);
    }

    [Fact]
    public async Task Run_command_records_workflow_instance_and_attempt_rows_linked_to_the_run()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-run-spine").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Spec");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var invocation = new TestApplicationInvocation(
            repository,
            new WorkflowInvocation(InvocationModeKind.BoundedTraditional),
            new TestApplicationCommand(TestApplicationCommandKind.Run, []));
        using var output = new StringWriter();
        using var error = new StringWriter();
        var runner = new UnifiedCliRunner(LoopRelayCompositionRoot.CreateForTests(repository), output, error);

        int exitCode = await runner.RunAsync(invocation, CancellationToken.None);

        Assert.Equal(0, exitCode);
        var store = new CanonicalWorkflowPersistenceStore(repository);
        RunRecord run = Assert.Single(await store.ReadRunsAsync());
        Assert.StartsWith("run_", run.RunId, StringComparison.Ordinal);
        Assert.Equal("Active", run.Status);
        Assert.Null(run.CompletedAt);
        WorkflowInstanceRecord instance = Assert.Single(await store.ReadWorkflowInstancesAsync());
        Assert.StartsWith("wfi_", instance.WorkflowInstanceId, StringComparison.Ordinal);
        Assert.Equal(run.RunId, instance.RunId);
        Assert.Equal(WorkflowIdentity.TraditionalRoadmap, instance.Workflow);
        Assert.Equal("Active", instance.Status);
        Assert.Equal(WorkflowStopReason.TransitionCompleted.ToString(), instance.Outcome);
        Assert.NotNull(instance.CompletedAt);
        AttemptRecord attempt = Assert.Single(await store.ReadAttemptsAsync());
        Assert.StartsWith("att_", attempt.AttemptId, StringComparison.Ordinal);
        Assert.StartsWith("tr_", attempt.TransitionRunId, StringComparison.Ordinal);
        Assert.Equal(instance.WorkflowInstanceId, attempt.WorkflowInstanceId);
        Assert.Equal(run.RunId, attempt.RunId);
        Assert.Equal(1, attempt.AttemptIndex);
        Assert.Equal(RuntimeOutcomeKind.Completed.ToString(), attempt.Outcome);
        Assert.NotNull(attempt.CompletedAt);
        CanonicalWorkflowPersistenceSnapshot snapshot = await store.LoadSnapshotAsync();
        CanonicalTransitionRunRecord transitionRun = Assert.Single(snapshot.TransitionRuns);
        Assert.Equal(transitionRun.RunId, attempt.TransitionRunId);
    }

    [Fact]
    public async Task Plan_revision_resumes_exact_authoring_thread_after_composition_restart()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-restart").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(repo), Path = repo };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        LoopRelayCompositionRoot first = LoopRelayCompositionRoot.CreateForTests(repository, runtime);
        TransitionRuntimeResult write = await RunPlanAsync(first, "Planning", "WriteExecutablePlan");
        PlanWarmSessionContinuity checkpoint = Assert.IsType<PlanWarmSessionContinuity>(
            await new CanonicalCheckpointStore(repository).ReadAsync<PlanWarmSessionContinuity>(CanonicalCheckpointKeys.PlanWarmSession, CancellationToken.None));
        Assert.Null(Assert.Single(runtime.OpenedSpecs).ResumeThreadId);
        await first.DisposeAsync();

        await WriteAdversarialReviewProductAsync(repository, "tighten after restart");
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.NotNull(spec.ResumeThreadId);
            Assert.Contains("tighten after restart", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v2");
            return new AgentTurnResult(1, AgentTurnState.Completed, "revised plan", AgentTokenUsage.Zero);
        }));
        await using LoopRelayCompositionRoot restarted = LoopRelayCompositionRoot.CreateForTests(repository, runtime);
        TransitionRuntimeResult revise = await RunPlanAsync(restarted, "Plan Validation", "RevisePlan");

        Assert.Equal(RuntimeOutcomeKind.Completed, write.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, revise.Outcome);
        Assert.Equal(2, runtime.OpenSessions);
        Assert.Equal(2, runtime.SessionCalls.Count);
        Assert.NotNull(runtime.OpenedSpecs[^1].ResumeThreadId);
        Assert.Equal("# Plan v2", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        await AssertCanonicalRecoveryPlanAsync(
            repository, $"plan:{checkpoint.ProviderThreadId}", CanonicalRecoveryAction.ResumeSession);
        Assert.Null(await new CanonicalCheckpointStore(repository).ReadAsync<PlanWarmSessionContinuity>(CanonicalCheckpointKeys.PlanWarmSession, CancellationToken.None));
    }

    [Fact]
    public async Task Verify_execute_entry_contract_completes_plan_and_persists_execution_readiness()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-verify").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan");
        await WriteAsync(repo, ".agents/operational_context.md", "# Operational Context");
        await WriteAsync(repo, ".agents/details.md", "# Details");
        await WriteAsync(repo, ".agents/milestones/m1.md", "# Milestone\n\n- [ ] Implement capability.");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var composition = LoopRelayCompositionRoot.CreateForTests(new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        });
        RepositoryObservation before = await composition.ObserveAsync(CancellationToken.None);
        WorkflowResolutionResult beforeResolution = composition.Resolve(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            before);

        TransitionRuntimeResult result = await RunPlanAsync(
            composition,
            "Workflow Completion",
            "VerifyExecuteEntryContract");

        RepositoryObservation after = await composition.ObserveAsync(CancellationToken.None);
        WorkflowResolutionResult afterResolution = composition.Resolve(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            after);

        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), beforeResolution.SelectedStage);
        Assert.Contains(beforeResolution.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("VerifyExecuteEntryContract") &&
            transition.State == TransitionEligibilityState.Eligible);
        Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation);
        Assert.Equal(TransitionDurableState.Completed, result.DurableState);
        ObservedWorkflowState state = Assert.Single(
            after.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.Plan);
        Assert.Equal(WorkflowResolutionState.Completed, state.State);
        Assert.Null(state.CurrentStage);
        Assert.Contains(
            after.Products,
            product => product.Product.Identity == ProductIdentity.ExecutionReadiness &&
                product.Product.ValidationState == ProductValidationState.Valid &&
                product.GateUsable);
        Assert.Equal(WorkflowResolutionState.Completed, afterResolution.WorkflowState);
        Assert.Equal(RepositoryClassification.Completed, afterResolution.Classification);
    }

    [Fact]
    public async Task Generate_operational_context_runs_as_deterministic_canonical_artifact_transition()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-operational-context").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan\n\nImplement the capability.");
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = LoopRelayCompositionRoot.CreateForTests(repository);

        TransitionRuntimeResult result = await RunPlanAsync(
            composition,
            "Execution Preparation",
            "GenerateOperationalContext");

        Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation);
        Assert.Equal(TransitionDurableState.Completed, result.DurableState);
        Assert.Equal(
            "# Plan\n\nImplement the capability.",
            await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "operational_context.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        ProductRecord product = Assert.Single(
            snapshot.Products,
            product => product.Identity == ProductIdentity.OperationalContext);
        Assert.Equal([OrchestrationArtifactPaths.OperationalContext], product.StorageRepresentations);
        Assert.Contains(
            snapshot.StageStates,
            stage => stage.Workflow == WorkflowIdentity.Plan &&
                stage.Stage == new WorkflowStageIdentity("Execution Preparation") &&
                stage.State == WorkflowResolutionState.Active);
        Assert.Contains(
            snapshot.WorkflowStates,
            state => state.Workflow == WorkflowIdentity.Plan &&
                state.State == WorkflowResolutionState.Resumable &&
                state.CurrentStage == new WorkflowStageIdentity("Execution Preparation"));
        CanonicalTransitionRunRecord run = Assert.Single(snapshot.TransitionRuns,
            item => item.Transition == new WorkflowTransitionIdentity("GenerateOperationalContext"));
        IReadOnlyList<EffectWorkItem> effects = await new CanonicalEffectWorkStore(repository)
            .ReadPlanAsync(new TransitionRunIdentity(run.RunId), CancellationToken.None);
        Assert.Equal(
            [
                "transition-effect:persist-operational-context",
                "filesystem:write:.agents/operational_context.md",
                "filesystem:write:.LoopRelay/evidence/local-artifacts/GenerateOperationalContext.md",
                "publication:derived-git-commit:Plan:GenerateOperationalContext:.agents/operational_context.md:nested-commit",
                "publication:derived-git-push:Plan:GenerateOperationalContext:.agents/operational_context.md:nested-push",
                "publication:derived-git-push:Plan:GenerateOperationalContext:.agents/operational_context.md:parent-gitlink-commit",
                "publication:derived-git-push:Plan:GenerateOperationalContext:.agents/operational_context.md:parent-push",
            ],
            effects.OrderBy(effect => effect.Intent.Order)
                .Select(effect => effect.Intent.SemanticOperationKey));
        Assert.All(effects, effect => Assert.Equal(EffectLifecycle.Succeeded, effect.State));
    }

    [Fact]
    public async Task Cancelled_turns_leave_terminal_turn_evidence_instead_of_vanishing_from_the_spine()
    {
        // M7: a turn that ends by exception (caller cancellation, transport failure) still
        // records a terminal turn row — otherwise its rendered fact would claim a send happened
        // while the turn spine showed nothing.
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-cancelled-turn").FullName;
        await WriteAsync(repo, ".agents/evals/e1.md", "# Eval Intent\n\nEvaluate the capability.");
        await WriteProjectContextAsync(repo);
        await GitWorkspace.InitializeWithAgentsInputsAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        LoopRelayCompositionRoot composition = LoopRelayCompositionRoot.CreateForTests(
            repository, new CancellingOneShotRuntime());

        TransitionRuntimeResult select = await RunEvalAsync(composition, "Evaluation Foundation", "SelectEvaluationIntent");
        Assert.True(select.AttemptCompleted, select.Explanation);
        // The executor converts the send's OperationCanceledException into the typed Cancelled
        // execution result — cancellation never collapses into generic failure.
        TransitionRuntimeResult cancelled = await RunEvalAsync(
            composition, "Dependency Inventory", "CreateEvalDependencyInventory");
        Assert.Equal(RuntimeOutcomeKind.Cancelled, cancelled.Outcome);

        var store = new CanonicalWorkflowPersistenceStore(repository);
        AgentSessionRecord session = Assert.Single(await store.ReadAgentSessionsAsync());
        Assert.NotNull(session.CompletedAt);
        AgentTurnRecord turn = Assert.Single(await store.ReadAgentTurnsAsync());
        Assert.Equal(session.SessionId, turn.SessionId);
        Assert.Equal("Canceled", turn.State);
        Assert.Equal("Cancelled", turn.DiagnosticsKind);
        Assert.Equal("cancelled mid-send", turn.Diagnostics);
        CanonicalRenderedPromptRecord fact = Assert.Single(
            await store.ReadRenderedPromptsAsync(),
            item => item.PromptIdentity == "CreateEvalDependencyInventory");
        Assert.Null(fact.SessionId);
        Assert.Null(fact.TurnId);
        Assert.Equal(turn.PromptSha256, fact.RenderedSha256);
    }
}
