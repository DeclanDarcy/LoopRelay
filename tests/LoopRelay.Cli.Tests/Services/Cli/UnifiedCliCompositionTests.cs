using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Cli.Services.Planning;
using LoopRelay.Cli.Tests.Models;
using LoopRelay.Cli.Tests.Services.Agents;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Projections.Models.ProjectionArtifacts;
using LoopRelay.Permissions.Models.Configuration;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class UnifiedCliCompositionTests
{
    [Fact]
    public void Execute_entry_rejects_milestone_cardinality_that_conflicts_with_strategic_context()
    {
        string root = Directory.CreateTempSubdirectory("looprelay-plan-cardinality").FullName;
        try
        {
            string context = Path.Combine(root, ".agents", "ctx");
            string milestones = Path.Combine(root, ".agents", "milestones");
            Directory.CreateDirectory(context);
            Directory.CreateDirectory(milestones);
            File.WriteAllText(
                Path.Combine(context, "04-strategic-structure.md"),
                "# Strategic Structure\n\nCreate exactly one implementation milestone.\n");
            File.WriteAllText(Path.Combine(milestones, "m1.md"), "# M1\n");
            File.WriteAllText(Path.Combine(milestones, "m2.md"), "# M2\n");

            Assert.True(UnifiedCliComposition.ExplicitSingleMilestoneInvariantViolated(root, out int actual));
            Assert.Equal(2, actual);

            File.Delete(Path.Combine(milestones, "m2.md"));
            Assert.False(UnifiedCliComposition.ExplicitSingleMilestoneInvariantViolated(root, out actual));
            Assert.Equal(1, actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_wires_canonical_observation_resolution_definitions_and_chains()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-composition").FullName;
        Directory.CreateDirectory(Path.Combine(repo, ".agents", "evals"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".agents", "evals", "e1.md"), "# Eval");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };

        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository);
        RepositoryObservation observation = await composition.ObserveAsync(CancellationToken.None);
        WorkflowResolutionResult resolution = composition.Resolve(
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            observation);
        WorkflowChainDefinition chain = composition.SelectChain(
            new WorkflowInvocation(InvocationModeKind.DefaultChained),
            observation);

        Assert.Same(repository, composition.Repository);
        Assert.IsType<TransitionRuntime>(composition.TransitionRuntime);
        Assert.Equal(4, composition.WorkflowDefinitions.Count);
        Assert.Equal(2, composition.WorkflowChains.Count);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, resolution.Selection.SelectedWorkflow);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, chain.InitialWorkflow);
        Assert.Equal(
            [WorkflowIdentity.EvalRoadmap, WorkflowIdentity.Plan, WorkflowIdentity.Execute],
            chain.Workflows.Select(workflow => workflow.Identity));
        Assert.True(observation.StorageAuthority.UsableAuthority);
    }

    [Fact]
    public void SelectChain_returns_single_workflow_chain_for_bounded_plan_and_execute()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-bounded").FullName;
        var composition = UnifiedCliComposition.Create(new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        });
        RepositoryObservation observation = EmptyObservation(repo);

        WorkflowChainDefinition plan = composition.SelectChain(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            observation);
        WorkflowChainDefinition execute = composition.SelectChain(
            new WorkflowInvocation(InvocationModeKind.BoundedExecute),
            observation);

        Assert.Equal([WorkflowIdentity.Plan], plan.Workflows.Select(workflow => workflow.Identity));
        Assert.Equal([WorkflowIdentity.Execute], execute.Workflows.Select(workflow => workflow.Identity));
    }

    [Fact]
    public async Task EvalRoadmap_milestone_deep_dive_blocks_empty_active_epic_context()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-eval-context").FullName;
        Directory.CreateDirectory(Path.Combine(repo, ".agents"));
        await File.WriteAllTextAsync(Path.Combine(repo, ".agents", "epic.md"), "   ");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = UnifiedCliComposition.Create(repository);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.EvalRoadmap,
                new WorkflowStageIdentity("Milestone Specification"),
                new WorkflowTransitionIdentity("GenerateMilestoneDeepDivesForEpic")));

        Assert.Equal(RuntimeOutcomeKind.Blocked, result.Outcome);
        Assert.Equal(TransitionDurableState.Blocked, result.DurableState);
        Assert.Contains("Active Epic prompt context is empty", result.Explanation, StringComparison.Ordinal);
        Assert.Contains(".agents/epic.md", result.Evidence);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        CanonicalBlockerRecord blocker = Assert.Single(snapshot.Blockers);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, blocker.Workflow);
        Assert.Equal(new WorkflowStageIdentity("Milestone Specification"), blocker.Stage);
        Assert.Equal(new WorkflowTransitionIdentity("GenerateMilestoneDeepDivesForEpic"), blocker.Transition);
        Assert.Equal(BlockerCategory.Transition, blocker.Blocker.Category);
        Assert.Contains(".agents/epic.md", blocker.Blocker.Evidence);
        CanonicalRecoveryMarkerRecord recovery = Assert.Single(snapshot.RecoveryMarkers);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, recovery.Workflow);
        Assert.Equal(new WorkflowStageIdentity("Milestone Specification"), recovery.Stage);
        Assert.Equal(new WorkflowTransitionIdentity("GenerateMilestoneDeepDivesForEpic"), recovery.Transition);
        Assert.Contains(".agents/epic.md", recovery.Evidence);
        CanonicalGateEvaluationRecord gate = Assert.Single(snapshot.GateEvaluations);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, gate.Workflow);
        Assert.Equal(new WorkflowStageIdentity("Milestone Specification"), gate.Stage);
        Assert.Equal(new WorkflowTransitionIdentity("GenerateMilestoneDeepDivesForEpic"), gate.Transition);
        Assert.Equal(GateStatus.Satisfied, gate.Status);
    }

    [Fact]
    public async Task Verify_execute_entry_contract_completes_plan_and_persists_execution_readiness()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-verify").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan");
        await WriteAsync(repo, ".agents/operational_context.md", "# Operational Context");
        await WriteAsync(repo, ".agents/details.md", "# Details");
        await WriteAsync(repo, ".agents/milestones/m1.md", "# Milestone\n\n- [ ] Implement capability.");
        var composition = UnifiedCliComposition.Create(new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        });
        RepositoryObservation before = await composition.ObserveAsync(CancellationToken.None);
        WorkflowResolutionResult beforeResolution = composition.Resolve(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            before);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Workflow Completion"),
                new WorkflowTransitionIdentity("VerifyExecuteEntryContract")));

        RepositoryObservation after = await composition.ObserveAsync(CancellationToken.None);
        WorkflowResolutionResult afterResolution = composition.Resolve(
            new WorkflowInvocation(InvocationModeKind.BoundedPlan),
            after);

        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), beforeResolution.SelectedStage);
        Assert.Contains(beforeResolution.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("VerifyExecuteEntryContract") &&
            transition.State == TransitionEligibilityState.Eligible);
        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
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
    public async Task Verify_execute_entry_contract_blocks_milestone_set_without_trackable_checkboxes()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-verify-milestone-checkbox").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan");
        await WriteAsync(repo, ".agents/operational_context.md", "# Operational Context");
        await WriteAsync(repo, ".agents/details.md", "# Details");
        await WriteAsync(repo, ".agents/milestones/m1.md", "# Milestone");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = UnifiedCliComposition.Create(repository);
        RepositoryObservation before = await composition.ObserveAsync(CancellationToken.None);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Workflow Completion"),
                new WorkflowTransitionIdentity("VerifyExecuteEntryContract")));

        ObservedProduct milestoneSet = Assert.Single(
            before.Products,
            product => product.Product.Identity == ProductIdentity.ExecutionMilestoneSet);
        Assert.False(milestoneSet.GateUsable);
        Assert.Equal(ProductValidationState.Invalid, milestoneSet.Product.ValidationState);
        Assert.Equal(RuntimeOutcomeKind.Blocked, result.Outcome);
        Assert.Equal(TransitionDurableState.Blocked, result.DurableState);
        Assert.Contains("Input gate blocked", result.Explanation, StringComparison.Ordinal);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        CanonicalBlockerRecord blocker = Assert.Single(snapshot.Blockers);
        Assert.Equal(BlockerCategory.Validation, blocker.Blocker.Category);
        Assert.Contains(".agents/milestones/m1.md", blocker.Blocker.Evidence);
    }

    [Fact]
    public async Task Generate_operational_context_runs_as_deterministic_canonical_artifact_transition()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-operational-context").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan\n\nImplement the capability.");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = UnifiedCliComposition.Create(repository);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Execution Preparation"),
                new WorkflowTransitionIdentity("GenerateOperationalContext")));

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
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
        Assert.Equal(
            [
                new EffectIdentity("persist-operational-context"),
                new EffectIdentity("publish-agents-operational-context"),
            ],
            snapshot.EffectRecords.Select(effect => effect.Effect));
        Assert.All(snapshot.EffectRecords, effect => Assert.Equal(EffectExecutionStatus.Succeeded, effect.Status));
    }

    [Fact]
    public async Task EvalRoadmap_prompt_transition_renders_generated_prompt_asset_before_executor_integration()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-eval-prompt").FullName;
        await WriteAsync(repo, ".agents/eval-architectural-catalog.md", "# Architectural Catalog");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = UnifiedCliComposition.Create(repository);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.EvalRoadmap,
                new WorkflowStageIdentity("Eval DAG"),
                new WorkflowTransitionIdentity("CreateEvalDag")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Contains("Prompt execution integration is not wired", result.Explanation, StringComparison.Ordinal);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        CanonicalTransitionEvidenceRecord started = Assert.Single(
            snapshot.TransitionEvidence,
            evidence => evidence.EventName == "TransitionStarted");
        Assert.Contains(
            started.Evidence,
            evidence => evidence.StartsWith("unified-cli/prompts/eval/CreateEvalDag.prompt@", StringComparison.Ordinal));
        Assert.Contains(
            started.Evidence,
            evidence => evidence.EndsWith(EvalPromptAssetCatalog.GetByTransition(new WorkflowTransitionIdentity("CreateEvalDag")).SourceHash, StringComparison.Ordinal));
    }

    [Fact]
    public async Task EvalRoadmap_workflow_transitions_run_through_canonical_runtime()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-eval-full-runtime").FullName;
        await WriteAsync(repo, ".agents/evals/e1.md", "# Eval Intent\n\nEvaluate the capability.");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        EnqueueEvalOutput(runtime, "CreateEvalDependencyInventory", "# Dependency Inventory");
        EnqueueEvalOutput(runtime, "CreateEvalHypothesisInventory", "# Hypothesis Inventory");
        EnqueueEvalOutput(runtime, "CreateArchitecturalCatalog", "# Architectural Catalog");
        EnqueueEvalOutput(runtime, "CreateEvalDag", "# Eval DAG");
        EnqueueEvalOutput(runtime, "CreateNextEpicRoadmap", "# Next Epic Roadmap");
        EnqueueEvalOutput(runtime, "CreateNextEpicImplementationSpec", ValidEvalActiveEpic());
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("Generate Milestone Deep Dives For Epic", prompt, StringComparison.Ordinal);
            Assert.Contains("Active Epic", prompt, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "specs"));
            File.WriteAllText(
                Path.Combine(repo, ".agents", "specs", "m1.md"),
                "# Milestone 1\n\nImplement the capability.");
            return new AgentTurnResult(6, AgentTurnState.Completed, "wrote milestone specs", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime);

        TransitionRuntimeResult select = await RunEvalAsync(composition, "Evaluation Foundation", "SelectEvaluationIntent");
        TransitionRuntimeResult dependencies = await RunEvalAsync(composition, "Dependency Inventory", "CreateEvalDependencyInventory");
        TransitionRuntimeResult hypotheses = await RunEvalAsync(composition, "Hypothesis Inventory", "CreateEvalHypothesisInventory");
        TransitionRuntimeResult catalog = await RunEvalAsync(composition, "Architectural Catalog", "CreateEvalArchitecturalCatalog");
        TransitionRuntimeResult dag = await RunEvalAsync(composition, "Eval DAG", "CreateEvalDag");
        TransitionRuntimeResult roadmap = await RunEvalAsync(composition, "Next Epic Roadmap", "CreateNextEpicRoadmap");
        TransitionRuntimeResult epic = await RunEvalAsync(composition, "Active Epic Preparation", "CreateNextEpicActiveEpic");
        TransitionRuntimeResult specs = await RunEvalAsync(composition, "Milestone Specification", "GenerateMilestoneDeepDivesForEpic");
        TransitionRuntimeResult verify = await RunEvalAsync(composition, "Workflow Completion", "VerifyPlanEntryContract");

        Assert.All(
            [select, dependencies, hypotheses, catalog, dag, roadmap, epic, specs, verify],
            result => Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation));
        Assert.Equal(7, runtime.OneShotCalls.Count);
        Assert.Equal(ValidEvalActiveEpic(), await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "epic.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "specs", "m1.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.WorkflowStates, state =>
            state.Workflow == WorkflowIdentity.EvalRoadmap &&
            state.State == WorkflowResolutionState.Completed &&
            state.CurrentStage is null);
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.PreparedEpic &&
            product.StorageRepresentations.Contains(EvaluationArtifactPaths.PreparedEpic));
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.MilestoneSpecificationSet &&
            product.StorageRepresentations.Contains(".agents/specs/m1.md"));
    }

    [Fact]
    public async Task TraditionalRoadmap_workflow_transitions_run_through_canonical_runtime()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-traditional-full-runtime").FullName;
        await WriteProjectContextAsync(repo);
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

                # FILE: .agents/specs/m1.md

                # Milestone Spec: Implement the roadmap capability

                ## Purpose

                Implement the roadmap capability.
                """, AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime);

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
            product.StorageRepresentations.Contains(".agents/specs/m1.md"));
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
        Assert.Contains(snapshot.EffectRecords, effect =>
            effect.Effect == new EffectIdentity("persist-prepared-epic") &&
            effect.Evidence.Contains(".LoopRelay/evidence/traditional-roadmap-effects/CreateEpic.md"));
    }

    [Fact]
    public async Task TraditionalRoadmap_invalid_prepared_epic_fails_with_recovery_marker()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-traditional-invalid-epic").FullName;
        await WriteProjectContextAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        EnqueueTraditionalOutput(runtime, "BootstrapRoadmapCompletionContext", "# Roadmap Completion Context");
        EnqueueTraditionalOutput(runtime, "SelectStrategicInitiative", "# Strategic Initiative Selection");
        EnqueueTraditionalOutput(runtime, "CreateNewEpic", "# Not An Epic\n\nThis cannot satisfy the prepared epic contract.");
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime);

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

        Assert.Equal(RuntimeOutcomeKind.Completed, context.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, selection.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Failed, epic.Outcome);
        Assert.Equal(TransitionDurableState.Failed, epic.DurableState);
        Assert.Contains("# Epic:", epic.Explanation, StringComparison.Ordinal);
        await composition.DisposeAsync();
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.DoesNotContain(snapshot.Products, product => product.Identity == ProductIdentity.PreparedEpic);
        Assert.Contains(snapshot.RecoveryMarkers, marker =>
            marker.Workflow == WorkflowIdentity.TraditionalRoadmap &&
            marker.Transition == new WorkflowTransitionIdentity("CreateEpic") &&
            marker.Evidence.Contains(".agents/epic.md"));
    }

    [Fact]
    public async Task Plan_prompt_transition_renders_generated_prompt_asset_before_executor_integration()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-prompt").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = UnifiedCliComposition.Create(repository);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Planning"),
                new WorkflowTransitionIdentity("WriteExecutablePlan")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Contains("Prompt execution integration is not wired", result.Explanation, StringComparison.Ordinal);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        CanonicalTransitionEvidenceRecord started = Assert.Single(
            snapshot.TransitionEvidence,
            evidence => evidence.EventName == "TransitionStarted");
        CanonicalPromptAsset asset = CanonicalPromptAssetCatalog.GetByPromptIdentity("WritePlan");
        Assert.Contains(
            started.Evidence,
            evidence => evidence == $"unified-cli/prompts/core/{asset.PromptAssetName}@{asset.SourceHash}");
    }

    [Fact]
    public async Task Plan_adversarial_review_context_starts_read_only_prompt_with_plan_and_projection_products()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-review-prompt").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan\n\nImplement capability.");
        await WriteAsync(
            repo,
            PlanPromptContext.AdversarialPlanReviewProjectionPath,
            "# Adversarial Plan Review Projection\n\nProject-specific review context.");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var composition = UnifiedCliComposition.Create(repository);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Plan Validation"),
                new WorkflowTransitionIdentity("RunAdversarialReview")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Contains("Prompt execution integration is not wired", result.Explanation, StringComparison.Ordinal);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        CanonicalTransitionEvidenceRecord started = Assert.Single(
            snapshot.TransitionEvidence,
            evidence => evidence.EventName == "TransitionStarted");
        CanonicalPromptAsset asset = CanonicalPromptAssetCatalog.GetByPromptIdentity("RunAdversarialReview");
        Assert.Contains(
            started.Evidence,
            evidence => evidence == $"unified-cli/prompts/core/{asset.PromptAssetName}@{asset.SourceHash}");
        CanonicalTransitionRunRecord run = Assert.Single(snapshot.TransitionRuns);
        Assert.Equal(TransitionDurableState.Failed, run.State);
        Assert.NotNull(run.InputSnapshotHash);
    }

    [Fact]
    public async Task Plan_warm_session_transitions_execute_and_reuse_one_authoring_session()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-session").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Prompt identity: WritePlan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Prompt identity: ReviewAndRevisePlan", prompt, StringComparison.Ordinal);
            Assert.Contains("Adversarial Review", prompt, StringComparison.Ordinal);
            Assert.Contains("tighten the plan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v2");
            return new AgentTurnResult(1, AgentTurnState.Completed, "revised plan", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime);

        TransitionRuntimeResult write = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Planning"),
                new WorkflowTransitionIdentity("WriteExecutablePlan")));
        await WriteAdversarialReviewProductAsync(repository, "tighten the plan");
        TransitionRuntimeResult revise = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Plan Validation"),
                new WorkflowTransitionIdentity("RevisePlan")));

        Assert.Equal(RuntimeOutcomeKind.Completed, write.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, revise.Outcome);
        Assert.Equal(1, runtime.OpenSessions);
        Assert.Equal(1, runtime.ClosedSessions);
        Assert.Equal(2, runtime.SessionCalls.Count);
        Assert.Equal("# Plan v2", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.ExecutablePlan &&
            product.ProducerTransition == new WorkflowTransitionIdentity("RevisePlan") &&
            product.CausalIdentity.Length == 64);
        Assert.Contains(snapshot.EffectRecords, effect =>
            effect.Effect == new EffectIdentity("persist-draft-plan") &&
            effect.Status == EffectExecutionStatus.Succeeded);
        Assert.Contains(snapshot.EffectRecords, effect =>
            effect.Effect == new EffectIdentity("persist-reviewed-plan") &&
            effect.Status == EffectExecutionStatus.Succeeded);
    }

    [Fact]
    public async Task Plan_revision_resumes_exact_authoring_thread_after_composition_restart()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-restart").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(repo), Path = repo };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition first = UnifiedCliComposition.Create(repository, runtime);
        TransitionRuntimeResult write = await RunPlanAsync(first, "Planning", "WriteExecutablePlan");
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
        await using UnifiedCliComposition restarted = UnifiedCliComposition.Create(repository, runtime);
        TransitionRuntimeResult revise = await RunPlanAsync(restarted, "Plan Validation", "RevisePlan");

        Assert.Equal(RuntimeOutcomeKind.Completed, write.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, revise.Outcome);
        Assert.Equal(2, runtime.OpenSessions);
        Assert.Equal(2, runtime.SessionCalls.Count);
        Assert.NotNull(runtime.OpenedSpecs[^1].ResumeThreadId);
        Assert.Equal("# Plan v2", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        Assert.Null(await new PlanWarmSessionContinuityStore(repository).ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Plan_revision_blocks_precisely_when_exact_thread_resume_fails()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-resume-fail").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(repo), Path = repo };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition first = UnifiedCliComposition.Create(repository, runtime);
        await RunPlanAsync(first, "Planning", "WriteExecutablePlan");
        await first.DisposeAsync();
        await WriteAdversarialReviewProductAsync(repository, "tighten after restart");
        runtime.FailResume = true;

        await using UnifiedCliComposition restarted = UnifiedCliComposition.Create(repository, runtime);
        TransitionRuntimeResult revise = await RunPlanAsync(restarted, "Plan Validation", "RevisePlan");

        Assert.Equal(RuntimeOutcomeKind.Blocked, revise.Outcome);
        Assert.Equal(TransitionDurableState.Blocked, revise.DurableState);
        Assert.Contains("could not resume the exact authoring thread", revise.Explanation, StringComparison.Ordinal);
        Assert.Equal("# Plan v1", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        Assert.Single(runtime.SessionCalls);
    }

    [Fact]
    public async Task Plan_warm_session_prompt_success_without_plan_file_fails_product_validation()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-warm-session-missing-plan").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "claimed success", AgentTokenUsage.Zero)));
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Planning"),
                new WorkflowTransitionIdentity("WriteExecutablePlan")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(TransitionDurableState.Failed, result.DurableState);
        Assert.Contains("completed without `.agents/plan.md`", result.Explanation, StringComparison.Ordinal);
        Assert.Equal(1, runtime.OpenSessions);
        await composition.DisposeAsync();
        Assert.Equal(1, runtime.ClosedSessions);
    }

    [Fact]
    public async Task Plan_warm_session_materializes_structurally_valid_returned_plan_when_tool_write_is_absent()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-returned-markdown").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(repo), Path = repo };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) => new AgentTurnResult(
            0,
            AgentTurnState.Completed,
            """
            # Executable Plan

            ## Milestone 1 — Implement Capability

            Implement the bounded repository capability, preserve its independent verifier, run the verifier,
            and record the exact acceptance result before checking the milestone completion item.
            """,
            AgentTokenUsage.Zero)));
        await using UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Planning"),
                new WorkflowTransitionIdentity("WriteExecutablePlan")));

        Assert.Equal(RuntimeOutcomeKind.Completed, result.Outcome);
        string plan = await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md"));
        Assert.StartsWith("# Executable Plan", plan, StringComparison.Ordinal);
        Assert.Contains("Milestone 1", plan, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Plan_workflow_transitions_run_through_canonical_runtime()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-full-runtime").FullName;
        await WriteAsync(repo, ".agents/epic.md", "# Active Epic");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        await WriteProjectContextAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Prompt identity: WritePlan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v1\n\nImplement capability.");
            return new AgentTurnResult(0, AgentTurnState.Completed, "wrote plan", AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Decision, spec.Role);
            Assert.Contains("AdversarialPlanReview", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(0, AgentTurnState.Completed, ValidAdversarialProjection(), AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Equal("read-only", spec.Sandbox.Identifier);
            Assert.Contains("# Adversarial Plan Review Projection", prompt, StringComparison.Ordinal);
            Assert.Contains("# Plan v1", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("{projectContextProjection}", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(
                1,
                AgentTurnState.Completed,
                "# Review\n\n## Verdict\n\n- CONDITIONAL PASS: tighten the plan.",
                AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Contains("Adversarial Review", prompt, StringComparison.Ordinal);
            Assert.Contains("tighten the plan", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v2\n\nImplement capability with gates.");
            return new AgentTurnResult(2, AgentTurnState.Completed, "revised plan", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal("collect-details", spec.OperationPermissionProfile?.Label);
            File.WriteAllText(Path.Combine(repo, ".agents", "details.md"), "# Details\n\nShared implementation detail.");
            return new AgentTurnResult(3, AgentTurnState.Completed, "details", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal("extract-milestones", spec.OperationPermissionProfile?.Label);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v3\n\nImplement capability with milestone gates.");
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "milestones"));
            File.WriteAllText(
                Path.Combine(repo, ".agents", "milestones", "m1.md"),
                "# Milestone 1\n\n- [ ] Implement canonical Plan runtime.");
            return new AgentTurnResult(4, AgentTurnState.Completed, "milestones", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal("extract-details", spec.OperationPermissionProfile?.Label);
            File.WriteAllText(Path.Combine(repo, ".agents", "details.md"), "# Details\n\nRefined shared implementation detail.");
            return new AgentTurnResult(5, AgentTurnState.Completed, "refined", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime);

        TransitionRuntimeResult write = await RunPlanAsync(composition, "Planning", "WriteExecutablePlan");
        TransitionRuntimeResult projection = await RunPlanAsync(composition, "Plan Validation", "GenerateAdversarialProjection");
        await AssertPlanStageAsync(repository, "Plan Validation", "GenerateAdversarialProjection");
        TransitionRuntimeResult review = await RunPlanAsync(composition, "Plan Validation", "RunAdversarialReview");
        await AssertPlanStageAsync(repository, "Plan Validation", "RunAdversarialReview");
        TransitionRuntimeResult revise = await RunPlanAsync(composition, "Plan Validation", "RevisePlan");
        await AssertPlanStageAsync(repository, "Execution Preparation", "RevisePlan");
        TransitionRuntimeResult operationalContext = await RunPlanAsync(composition, "Execution Preparation", "GenerateOperationalContext");
        TransitionRuntimeResult details = await RunPlanAsync(composition, "Execution Preparation", "CollectExecutionDetails");
        await AssertPlanStageAsync(repository, "Execution Preparation", "CollectExecutionDetails");
        TransitionRuntimeResult milestones = await RunPlanAsync(composition, "Execution Preparation", "GenerateExecutionMilestones");
        await AssertPlanStageAsync(repository, "Execution Preparation", "GenerateExecutionMilestones");
        TransitionRuntimeResult refine = await RunPlanAsync(composition, "Execution Preparation", "RefineExecutionDetails");
        await AssertPlanStageAsync(repository, "Workflow Completion", "RefineExecutionDetails");
        TransitionRuntimeResult verify = await RunPlanAsync(composition, "Workflow Completion", "VerifyExecuteEntryContract");

        Assert.All(
            [write, projection, review, revise, operationalContext, details, milestones, refine, verify],
            result => Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation));
        Assert.Single(runtime.OneShotCalls);
        Assert.Equal(5, runtime.OpenSessions);
        Assert.Equal(5, runtime.ClosedSessions);
        Assert.Equal(6, runtime.SessionCalls.Count);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.AdversarialProjection &&
            product.StorageRepresentations.Contains(PlanPromptContext.AdversarialPlanReviewProjectionPath));
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.AdversarialReview &&
            product.StorageRepresentations.Contains(".LoopRelay/evidence/plan/adversarial-review.md"));
        Assert.Contains(snapshot.WorkflowStates, state =>
            state.Workflow == WorkflowIdentity.Plan &&
            state.State == WorkflowResolutionState.Completed &&
            state.CurrentStage is null);
        Assert.Contains(snapshot.Products, product => product.Identity == ProductIdentity.ExecutionReadiness);
    }

    [Fact]
    public async Task Execute_handoff_resumes_exact_implementation_thread_and_restores_slice_facts_after_restart()
    {
        (string repo, Repository repository, FakeAgentRuntime runtime, FakeProcessRunner process) = await PrepareExecuteContinuityCaseAsync(
            "cc-cli-unified-execute-warm-restart");
        await WriteAsync(repo, "README.md", "# Canonical Capability\n\nUse exact repository bytes.");
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nCreate src/feature.cs.", AgentTokenUsage.Zero)));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Null(spec.ResumeThreadId);
            Assert.Contains("# Repository README Context", prompt, StringComparison.Ordinal);
            Assert.Contains("Use exact repository bytes.", prompt, StringComparison.Ordinal);
            Assert.Contains("the README resolves the canonical target", prompt, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.Combine(repo, "src"));
            File.WriteAllText(Path.Combine(repo, "src", "feature.cs"), "feature\n");
            return new AgentTurnResult(1, AgentTurnState.Completed, "implemented", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition first = UnifiedCliComposition.Create(repository, runtime, process);
        await RunPlanAsync(first, "Workflow Completion", "VerifyExecuteEntryContract");
        await RunExecuteAsync(first, "Execution Readiness", "VerifyExecutionReadiness");
        await RunExecuteAsync(first, "Implementation Planning", "GenerateDecision");
        TransitionRuntimeResult implementation = await RunExecuteAsync(
            first, "Implementation", "ExecuteImplementationSlice");
        ExecutionWarmSessionContinuity checkpoint = Assert.IsType<ExecutionWarmSessionContinuity>(
            await new ExecutionWarmSessionContinuityStore(repository).ReadAsync(CancellationToken.None));
        await first.DisposeAsync();

        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, _, _) =>
        {
            Assert.Equal(checkpoint.ProviderThreadId, spec.ResumeThreadId);
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "handoffs"));
            File.WriteAllText(Path.Combine(repo, ".agents", "handoffs", "handoff.md"), "# Handoff\n\nDone.\n");
            return new AgentTurnResult(2, AgentTurnState.Completed, "handoff", AgentTokenUsage.Zero);
        }));
        await using UnifiedCliComposition restarted = UnifiedCliComposition.Create(repository, runtime, process);
        TransitionRuntimeResult handoff = await RunExecuteAsync(
            restarted, "Execution Continuity", "GenerateHandoff");

        Assert.Equal(RuntimeOutcomeKind.Completed, implementation.Outcome);
        Assert.Equal(RuntimeOutcomeKind.Completed, handoff.Outcome);
        Assert.Equal(checkpoint.ProviderThreadId, runtime.OpenedSpecs[^1].ResumeThreadId);
        ExecutionWarmSessionContinuity after = Assert.IsType<ExecutionWarmSessionContinuity>(
            await new ExecutionWarmSessionContinuityStore(repository).ReadAsync(CancellationToken.None));
        Assert.True(after.HandoffCompleted);
        Assert.Equal(checkpoint.SliceBaseline.ExecutionSliceId, after.SliceBaseline.ExecutionSliceId);
    }

    [Fact]
    public async Task Execute_handoff_blocks_precisely_when_exact_implementation_thread_resume_fails()
    {
        (string repo, Repository repository, FakeAgentRuntime runtime, FakeProcessRunner process) = await PrepareExecuteContinuityCaseAsync(
            "cc-cli-unified-execute-warm-resume-fail");
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
            new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nCreate src/feature.cs.", AgentTokenUsage.Zero)));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            Directory.CreateDirectory(Path.Combine(repo, "src"));
            File.WriteAllText(Path.Combine(repo, "src", "feature.cs"), "feature\n");
            return new AgentTurnResult(1, AgentTurnState.Completed, "implemented", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition first = UnifiedCliComposition.Create(repository, runtime, process);
        await RunPlanAsync(first, "Workflow Completion", "VerifyExecuteEntryContract");
        await RunExecuteAsync(first, "Execution Readiness", "VerifyExecutionReadiness");
        await RunExecuteAsync(first, "Implementation Planning", "GenerateDecision");
        await RunExecuteAsync(first, "Implementation", "ExecuteImplementationSlice");
        await first.DisposeAsync();
        runtime.FailResume = true;

        await using UnifiedCliComposition restarted = UnifiedCliComposition.Create(repository, runtime, process);
        TransitionRuntimeResult handoff = await RunExecuteAsync(
            restarted, "Execution Continuity", "GenerateHandoff");

        Assert.Equal(RuntimeOutcomeKind.Blocked, handoff.Outcome);
        Assert.Equal(TransitionDurableState.Blocked, handoff.DurableState);
        Assert.Contains("could not resume the exact execution thread", handoff.Explanation, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(repo, ".agents", "handoffs", "handoff.md")));
    }

    private static async Task AssertPlanStageAsync(
        Repository repository,
        string expectedStage,
        string completedTransition)
    {
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        CanonicalWorkflowStateRecord workflow = Assert.Single(
            snapshot.WorkflowStates,
            state => state.Workflow == WorkflowIdentity.Plan);
        Assert.Equal(new WorkflowStageIdentity(expectedStage), workflow.CurrentStage);
        Assert.Contains(snapshot.TransitionRuns, run =>
            run.Transition == new WorkflowTransitionIdentity(completedTransition) &&
            run.State == TransitionDurableState.Completed);
    }

    [Fact]
    public async Task Plan_scoped_artifact_transitions_execute_with_operation_profiles_and_persist_products()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-scoped-artifacts").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan v1\n\nImplement capability.");
        await WriteAsync(repo, ".agents/specs/s1.md", "# Milestone Spec");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Equal("collect-details", spec.OperationPermissionProfile?.Label);
            Assert.Contains("Scoped Operation Contract", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "details.md"), "# Details\n\nShared implementation detail.");
            return new AgentTurnResult(0, AgentTurnState.Completed, "details", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal("extract-milestones", spec.OperationPermissionProfile?.Label);
            Assert.Contains("Required output glob: .agents/milestones/m*.md", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan v2\n\nRewritten with milestone markers.");
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "milestones"));
            File.WriteAllText(
                Path.Combine(repo, ".agents", "milestones", "m1.md"),
                "# Milestone 1\n\n- [ ] Implement scoped artifact runtime.");
            return new AgentTurnResult(1, AgentTurnState.Completed, "milestones", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal("extract-details", spec.OperationPermissionProfile?.Label);
            Assert.Contains(".agents/milestones/m*.md", prompt, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(repo, ".agents", "details.md"), "# Details\n\nRefined shared implementation detail.");
            return new AgentTurnResult(2, AgentTurnState.Completed, "refined", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime);

        TransitionRuntimeResult collect = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Execution Preparation"),
                new WorkflowTransitionIdentity("CollectExecutionDetails")));
        TransitionRuntimeResult milestones = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Execution Preparation"),
                new WorkflowTransitionIdentity("GenerateExecutionMilestones")));
        TransitionRuntimeResult refine = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Execution Preparation"),
                new WorkflowTransitionIdentity("RefineExecutionDetails")));

        Assert.True(collect.Outcome == RuntimeOutcomeKind.Completed, collect.Explanation);
        Assert.True(milestones.Outcome == RuntimeOutcomeKind.Completed, milestones.Explanation);
        Assert.True(refine.Outcome == RuntimeOutcomeKind.Completed, refine.Explanation);
        Assert.Equal(3, runtime.OpenSessions);
        Assert.Equal(3, runtime.ClosedSessions);
        Assert.Equal(3, runtime.SessionCalls.Count);
        Assert.Collection(
            runtime.OpenedSpecs.Select(spec => spec.OperationPermissionProfile),
            profile =>
            {
                Assert.NotNull(profile);
                Assert.Equal("collect-details", profile.Label);
                Assert.Equal([OrchestrationArtifactPaths.Plan], profile.AllowedReads);
                Assert.Equal([OrchestrationArtifactPaths.Details], profile.AllowedWrites);
            },
            profile =>
            {
                Assert.NotNull(profile);
                Assert.Equal("extract-milestones", profile.Label);
                Assert.Equal([OrchestrationArtifactPaths.Plan, OrchestrationArtifactPaths.Details], profile.AllowedReads);
                Assert.Equal([OrchestrationArtifactPaths.Plan], profile.AllowedWrites);
                Assert.Equal(".agents/milestones", Assert.Single(profile.AllowedWriteGlobs).Directory);
            },
            profile =>
            {
                Assert.NotNull(profile);
                Assert.Equal("extract-details", profile.Label);
                Assert.Equal([OrchestrationArtifactPaths.Details], profile.AllowedReads);
                Assert.Equal([OrchestrationArtifactPaths.Details], profile.AllowedWrites);
                Assert.Equal(".agents/milestones", Assert.Single(profile.AllowedWriteGlobs).Directory);
            });
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.ExecutionDetails &&
            product.ProducerTransition == new WorkflowTransitionIdentity("RefineExecutionDetails") &&
            product.CausalIdentity.Length == 64);
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.ExecutionMilestoneSet &&
            product.ProducerTransition == new WorkflowTransitionIdentity("GenerateExecutionMilestones") &&
            product.StorageRepresentations.Contains(".agents/milestones/m1.md"));
        Assert.Contains(snapshot.EffectRecords, effect =>
            effect.Effect == new EffectIdentity("persist-execution-details") &&
            effect.Status == EffectExecutionStatus.Succeeded);
        Assert.Contains(snapshot.EffectRecords, effect =>
            effect.Effect == new EffectIdentity("persist-execution-milestones") &&
            effect.Status == EffectExecutionStatus.Succeeded);
        Assert.True(File.Exists(Path.Combine(
            repo,
            ".LoopRelay",
            "evidence",
            "plan-scoped-artifact",
            "RefineExecutionDetails.md")));
    }

    [Fact]
    public async Task Plan_scoped_artifact_milestone_without_checkboxes_rolls_back_declared_writes()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-plan-scoped-rollback").FullName;
        await WriteAsync(repo, ".agents/plan.md", "# Plan original\n\nImplement capability.");
        await WriteAsync(repo, ".agents/details.md", "# Details\n\nUniversal detail.");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        runtime.SessionTurns.Enqueue(new ScriptedTurn((_, _, _) =>
        {
            File.WriteAllText(Path.Combine(repo, ".agents", "plan.md"), "# Plan rewritten without valid milestones.");
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "milestones"));
            File.WriteAllText(Path.Combine(repo, ".agents", "milestones", "m1.md"), "# Milestone without checkbox");
            return new AgentTurnResult(0, AgentTurnState.Completed, "claimed success", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime);

        TransitionRuntimeResult result = await composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity("Execution Preparation"),
                new WorkflowTransitionIdentity("GenerateExecutionMilestones")));

        Assert.Equal(RuntimeOutcomeKind.Failed, result.Outcome);
        Assert.Equal(TransitionDurableState.Failed, result.DurableState);
        Assert.Contains("no trackable checkboxes", result.Explanation, StringComparison.Ordinal);
        Assert.Equal("# Plan original\n\nImplement capability.", await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "plan.md")));
        Assert.False(File.Exists(Path.Combine(repo, ".agents", "milestones", "m1.md")));
        Assert.Equal(1, runtime.OpenSessions);
        Assert.Equal(1, runtime.ClosedSessions);
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.DoesNotContain(snapshot.Products, product => product.Identity == ProductIdentity.ExecutionMilestoneSet);
    }

    [Fact]
    public async Task Execute_workflow_transitions_run_through_canonical_runtime()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-execute-full-runtime").FullName;
        await WriteAsync(repo, ".agents/epic.md", ValidEvalActiveEpic());
        await WriteAsync(repo, ".agents/core/roadmap-completion-context.md", "# Roadmap Completion Context\n\nCurrent.");
        await WriteAsync(repo, ".agents/plan.md", "# Plan\n\nImplement the runtime capability.");
        await WriteAsync(repo, ".agents/operational_context.md", "# Operational Context\n\nUse the plan.");
        await WriteAsync(repo, ".agents/operational_delta.md", "# Operational Delta\n\nUpdate context.");
        await WriteAsync(repo, ".agents/details.md", "# Details\n\nImplementation detail.");
        await WriteAsync(repo, ".agents/handoffs/handoff.md", "# Prior Handoff\n\nPrevious slice.");
        await WriteAsync(repo, ".agents/milestones/m1.md", "# Milestone 1\n\n- [ ] Implement Execute runtime.");
        await WriteProjectContextAsync(repo);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        var git = new FakeProcessRunner
        {
            Handler = (workingDirectory, args) =>
            {
                if (args.SequenceEqual(["status", "--porcelain"]))
                {
                    return FakeProcessRunner.Ok(
                        workingDirectory.EndsWith(".agents", StringComparison.OrdinalIgnoreCase)
                            ? " M handoffs/handoff.md\n"
                            : " M src/feature.cs\n");
                }

                if (args.SequenceEqual(["status", "--porcelain", "--untracked-files=all"]))
                {
                    return FakeProcessRunner.Ok(" M src/feature.cs\n");
                }

                if (args.SequenceEqual(["diff", "--name-status", "--find-renames", "HEAD", "--"]))
                {
                    return FakeProcessRunner.Ok("M\tsrc/feature.cs\n");
                }

                if (args.SequenceEqual(["branch", "--show-current"]))
                {
                    return FakeProcessRunner.Ok("main\n");
                }

                if (args.Count >= 2 && args[0] == "rev-parse")
                {
                    return FakeProcessRunner.Ok("abc123\n");
                }

                if (args.SequenceEqual(["rev-list", "--count", "@{u}..HEAD"]))
                {
                    return FakeProcessRunner.Ok("0\n");
                }

                return FakeProcessRunner.Ok();
            },
        };
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Decision, spec.Role);
            Assert.Contains("Operational Context", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(0, AgentTurnState.Completed, "# Decisions\n\nImplement feature.cs.", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.Contains("# Decisions", prompt, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.Combine(repo, "src"));
            File.WriteAllText(Path.Combine(repo, "src", "feature.cs"), "namespace Test; public static class Feature { }\n");
            File.WriteAllText(
                Path.Combine(repo, ".agents", "milestones", "m1.md"),
                "# Milestone 1\n\n- [x] Implement Execute runtime.");
            return new AgentTurnResult(1, AgentTurnState.Completed, "implemented feature", AgentTokenUsage.Zero);
        }));
        runtime.SessionTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("handoff", prompt, StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(Path.Combine(repo, ".agents", "handoffs"));
            File.WriteAllText(
                Path.Combine(repo, ".agents", "handoffs", "handoff.md"),
                "# Handoff\n\nFeature implemented.");
            return new AgentTurnResult(2, AgentTurnState.Completed, "wrote handoff", AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Decision, spec.Role);
            Assert.Contains("EvaluateEpicCompletionAndDrift", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(3, AgentTurnState.Completed, ValidProjection("# Epic Completion Evaluation Projection", "EvaluateEpicCompletionAndDrift"), AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.Contains("Evaluate Epic Completion And Drift", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(4, AgentTurnState.Completed, Evaluation("Fully Complete", "None", "Close Epic"), AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.Contains("Epic Synthesis Prompt", prompt, StringComparison.Ordinal);
            Assert.Contains("trusted runtime owns persistence", prompt, StringComparison.OrdinalIgnoreCase);
            return new AgentTurnResult(5, AgentTurnState.Completed, """
                # Completed Epic

                ## 1. Epic Purpose

                Preserve the completed capability.

                ## 2. Current State

                Capability complete.
                """, AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Decision, spec.Role);
            Assert.Contains("UpdateRoadmapCompletionContext", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(6, AgentTurnState.Completed, ValidProjection("# Roadmap Completion Update Projection", "UpdateRoadmapCompletionContext"), AgentTokenUsage.Zero);
        }));
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.Planning, spec.Role);
            Assert.Equal("danger-full-access", spec.Sandbox.Identifier);
            Assert.Contains("Update Roadmap Completion Context", prompt, StringComparison.Ordinal);
            return new AgentTurnResult(7, AgentTurnState.Completed, "# Roadmap Completion Context\n\nUpdated.", AgentTokenUsage.Zero);
        }));
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime, git);

        TransitionRuntimeResult planReady = await RunPlanAsync(composition, "Workflow Completion", "VerifyExecuteEntryContract");
        TransitionRuntimeResult readiness = await RunExecuteAsync(composition, "Execution Readiness", "VerifyExecutionReadiness");
        WorkflowControllerResult readinessController = await composition.WorkflowController.RunAsync(
            new WorkflowControllerRequest(
                new WorkflowInvocation(InvocationModeKind.BoundedExecute),
                await composition.ObserveAsync(CancellationToken.None),
                composition.WorkflowDefinitions));
        TransitionRuntimeResult decision = Assert.IsType<TransitionRuntimeResult>(readinessController.Transition);
        Assert.True(
            readinessController.StopReason == WorkflowStopReason.TransitionCompleted,
            readinessController.Explanation);
        Assert.Equal(new WorkflowTransitionIdentity("GenerateDecision"), decision.Transition);
        TransitionRuntimeResult implementation = await RunExecuteAsync(composition, "Implementation", "ExecuteImplementationSlice");
        TransitionRuntimeResult handoff = await RunExecuteAsync(composition, "Execution Continuity", "GenerateHandoff");
        TransitionRuntimeResult updateContext = await RunExecuteAsync(composition, "Execution Continuity", "UpdateOperationalContext");
        TransitionRuntimeResult publish = await RunExecuteAsync(composition, "Execution Continuity", "PublishRepositoryState");
        TransitionRuntimeResult commit = await RunExecuteAsync(composition, "Execution Continuity", "EvaluateCommit");
        TransitionRuntimeResult milestones = await RunExecuteAsync(composition, "Completion", "EvaluateMilestoneCompletion");
        TransitionRuntimeResult review = await RunExecuteAsync(composition, "Completion", "RunNonImplementationReview");
        TransitionRuntimeResult certification = await RunExecuteAsync(composition, "Completion", "RunCompletionCertification");
        Assert.NotNull(await new CompletionCertificationCheckpointStore(repository).ReadAsync(CancellationToken.None));
        await composition.DisposeAsync();
        composition = UnifiedCliComposition.Create(repository, runtime, git);
        TransitionRuntimeResult route = await RunExecuteAsync(composition, "Completion", "InterpretCompletionRoute");
        await composition.DisposeAsync();
        composition = UnifiedCliComposition.Create(repository, runtime, git);
        WorkflowControllerResult exitController = await composition.WorkflowController.RunAsync(
            new WorkflowControllerRequest(
                new WorkflowInvocation(InvocationModeKind.BoundedExecute),
                await composition.ObserveAsync(CancellationToken.None),
                composition.WorkflowDefinitions));
        TransitionRuntimeResult exit = Assert.IsType<TransitionRuntimeResult>(exitController.Transition);
        Assert.True(
            exitController.StopReason == WorkflowStopReason.TransitionCompleted,
            exitController.Explanation + " | " + exit.Explanation);
        Assert.Equal(new WorkflowTransitionIdentity("VerifyWorkflowExitGate"), exit.Transition);

        Assert.All(
            [planReady, readiness, decision, implementation, handoff, updateContext, publish, commit, milestones, review, certification, route, exit],
            result => Assert.True(result.Outcome == RuntimeOutcomeKind.Completed, result.Explanation));
        await composition.DisposeAsync();
        Assert.Equal(2, runtime.OpenSessions);
        Assert.Equal(2, runtime.ClosedSessions);
        Assert.Equal(4, runtime.SessionCalls.Count);
        Assert.Equal(5, runtime.OneShotCalls.Count);
        AgentSessionSpec decisionSpec = runtime.OpenedSpecs.Single(spec => spec.Role == SessionRole.Decision);
        Assert.Equal(AgentModel.Gpt56Sol, decisionSpec.Model);
        Assert.Equal(AgentEffort.XHigh, decisionSpec.Effort);
        Assert.Equal(AgentConfigurationAuthority.Brain, decisionSpec.ConfigurationAuthority);
        AgentSessionSpec executionSpec = runtime.OpenedSpecs.Single(
            spec => spec.ConfigurationAuthority == AgentConfigurationAuthority.Execution);
        Assert.Equal(AgentModel.Gpt56Terra, executionSpec.Model);
        Assert.Equal(AgentEffort.High, executionSpec.Effort);
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1", "handoffs", "handoff.0001.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1", "handoffs", "handoff.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1", "deltas", "operational_delta.0001.md")));
        Assert.True(File.Exists(Path.Combine(repo, ".agents", "archive", "epics", "1", "decisions", "decisions.0001.md")));
        Assert.Equal(
            "# Roadmap Completion Context\n\nUpdated.",
            await File.ReadAllTextAsync(Path.Combine(repo, ".agents", "core", "roadmap-completion-context.md")));
        string recoveryDirectory = Path.Combine(repo, ".LoopRelay", "evidence", "execute-completion-recovery");
        string recoveryEvidence = string.Join(
            "\n",
            Directory.GetFiles(recoveryDirectory, "*.md").Select(File.ReadAllText));
        Assert.Contains("Completion review", recoveryEvidence, StringComparison.Ordinal);
        Assert.Contains("Evaluate epic completion and drift", recoveryEvidence, StringComparison.Ordinal);
        Assert.Contains("Archive completed execution workspace", recoveryEvidence, StringComparison.Ordinal);
        Assert.Contains("Synthesize completed epic", recoveryEvidence, StringComparison.Ordinal);
        Assert.Contains("Update roadmap completion context", recoveryEvidence, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(recoveryDirectory, "final-closed-state-persistence.md")));
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.WorkflowStates, state =>
            state.Workflow == WorkflowIdentity.Execute &&
            state.State == WorkflowResolutionState.Completed &&
            state.CurrentStage is null);
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.CertifiedCompletion &&
            product.StorageRepresentations.Contains(".LoopRelay/evidence/local-verification/VerifyWorkflowExitGate.md"));
        Assert.Contains(snapshot.Products, product =>
            product.Identity == ProductIdentity.CompletionRoute &&
            product.StorageRepresentations.Contains(".LoopRelay/evidence/execute-review/InterpretCompletionRoute-output.md"));
        Assert.Contains(snapshot.EffectRecords, effect =>
            effect.Effect == new EffectIdentity("record-completion-evidence") &&
            effect.Status == EffectExecutionStatus.Succeeded);
        Assert.Null(await new CompletionCertificationCheckpointStore(repository).ReadAsync(CancellationToken.None));
        Assert.Null(await new ExecutionWarmSessionContinuityStore(repository).ReadAsync(CancellationToken.None));

        int sessionCallsBeforeRerun = runtime.SessionCalls.Count;
        int oneShotCallsBeforeRerun = runtime.OneShotCalls.Count;
        UnifiedCliComposition rerunComposition = UnifiedCliComposition.Create(repository, runtime, git);
        WorkflowControllerResult rerun = await rerunComposition.WorkflowController.RunAsync(
            new WorkflowControllerRequest(
                new WorkflowInvocation(InvocationModeKind.BoundedExecute),
                await rerunComposition.ObserveAsync(CancellationToken.None),
                rerunComposition.WorkflowDefinitions));
        await rerunComposition.DisposeAsync();
        Assert.Equal(WorkflowStopReason.ChainCompleted, rerun.StopReason);
        Assert.Null(rerun.Transition);
        Assert.Equal(sessionCallsBeforeRerun, runtime.SessionCalls.Count);
        Assert.Equal(oneShotCallsBeforeRerun, runtime.OneShotCalls.Count);
    }

    [Fact]
    public async Task Execute_commit_evaluation_stall_persists_canonical_evidence()
    {
        string repo = Directory.CreateTempSubdirectory("cc-cli-unified-execute-stall").FullName;
        await WriteAsync(repo, ".agents/milestones/m1.md", "# Milestone 1\n\n- [ ] Implement Execute runtime.");
        await WriteAsync(
            repo,
            ".LoopRelay/evidence/execute-stall/state.md",
            """
            # Execute Stall State

            | Field | Value |
            |---|---|
            | Consecutive No-Progress Count | 2 |
            | Stalled | False |
            """);
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        await WriteRepositoryChangesProductAsync(repository);
        var runtime = new FakeAgentRuntime(new MemoryArtifactStore());
        var git = new FakeProcessRunner
        {
            Handler = (workingDirectory, args) => args.SequenceEqual(["status", "--porcelain"])
                ? FakeProcessRunner.Ok(" M .agents\n")
                : FakeProcessRunner.Ok(),
        };
        UnifiedCliComposition composition = UnifiedCliComposition.Create(repository, runtime, git);

        TransitionRuntimeResult result = await RunExecuteAsync(
            composition,
            "Execution Continuity",
            "EvaluateCommit");

        Assert.Equal(RuntimeOutcomeKind.Stalled, result.Outcome);
        Assert.Equal(TransitionDurableState.Stalled, result.DurableState);
        Assert.Contains(".LoopRelay/evidence/execute-stall/state.md", result.Evidence);
        await composition.DisposeAsync();
        CanonicalWorkflowPersistenceSnapshot snapshot =
            await new CanonicalWorkflowPersistenceStore(repository).LoadSnapshotAsync();
        Assert.Contains(snapshot.TransitionRuns, run =>
            run.Transition == new WorkflowTransitionIdentity("EvaluateCommit") &&
            run.State == TransitionDurableState.Stalled &&
            run.Outcome == RuntimeOutcomeKind.Stalled &&
            run.Evidence.Contains(".LoopRelay/evidence/execute-stall/state.md"));
        Assert.Contains(snapshot.EffectRecords, effect =>
            effect.Effect == new EffectIdentity("record-commit-evaluation") &&
            effect.Status == EffectExecutionStatus.Stalled);
        Assert.Contains(snapshot.Blockers, blocker =>
            blocker.Workflow == WorkflowIdentity.Execute &&
            blocker.Transition == new WorkflowTransitionIdentity("EvaluateCommit") &&
            blocker.Blocker.Category == BlockerCategory.Repository &&
            blocker.Blocker.Evidence.Contains(".LoopRelay/evidence/execute-stall/state.md"));
        Assert.Contains(snapshot.RecoveryMarkers, marker =>
            marker.Workflow == WorkflowIdentity.Execute &&
            marker.Transition == new WorkflowTransitionIdentity("EvaluateCommit") &&
            marker.MarkerId.Contains("Stalled", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_cli_composition_does_not_construct_legacy_loop_runner()
    {
        string root = FindRepositoryRoot();
        string cliSource = Path.Combine(root, "src", "LoopRelay.Cli");
        string[] productionSources = Directory
            .EnumerateFiles(cliSource, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(
                Path.Combine("Services", "Execution", "LoopRunner.cs"),
                StringComparison.Ordinal))
            .ToArray();

        Assert.DoesNotContain(
            productionSources,
            path => File.ReadAllText(path).Contains("new LoopRunner", StringComparison.Ordinal));
        Assert.DoesNotContain(
            productionSources,
            path => File.ReadAllText(path).Contains("RoadmapStateMachine", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(cliSource, "Services", "Cli", "LoopCliComposition.cs")));
    }

    [Fact]
    public void Retired_plan_and_roadmap_compositions_are_not_available_as_active_authorities()
    {
        string root = FindRepositoryRoot();

        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "LoopRelay.Plan.Cli",
            "Services",
            "Cli",
            "PlanCliComposition.cs")));
        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "LoopRelay.Roadmap.Cli",
            "Services",
            "Cli",
            "RoadmapCliComposition.cs")));
    }

    private static RepositoryObservation EmptyObservation(string repo)
    {
        var storage = new StorageVerificationResult(
            StorageAuthorityKind.Missing,
            UsableAuthority: true,
            StaleExports: [],
            Conflicts: [],
            Corruption: [],
            UnsupportedSchema: [],
            UnresolvedReferences: [],
            PartialTransactions: [],
            BlockingConditions: [],
            Evidence: []);
        return new RepositoryObservation(
            repo,
            new StorageAuthoritySnapshot(storage.Authority, storage.UsableAuthority, "test", []),
            WorkflowStates: [],
            Products: [],
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(IsRepository: false, HasWorkingTreeChanges: false, CurrentBranch: "unknown", Evidence: []),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: [],
            StorageVerification: storage);
    }

    private static async Task WriteAsync(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static Task<TransitionRuntimeResult> RunPlanAsync(
        UnifiedCliComposition composition,
        string stage,
        string transition) =>
        composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Plan,
                new WorkflowStageIdentity(stage),
                new WorkflowTransitionIdentity(transition)));

    private static Task<TransitionRuntimeResult> RunEvalAsync(
        UnifiedCliComposition composition,
        string stage,
        string transition) =>
        composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.EvalRoadmap,
                new WorkflowStageIdentity(stage),
                new WorkflowTransitionIdentity(transition)));

    private static Task<TransitionRuntimeResult> RunTraditionalAsync(
        UnifiedCliComposition composition,
        string stage,
        string transition) =>
        composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.TraditionalRoadmap,
                new WorkflowStageIdentity(stage),
                new WorkflowTransitionIdentity(transition)));

    private static Task<TransitionRuntimeResult> RunExecuteAsync(
        UnifiedCliComposition composition,
        string stage,
        string transition) =>
        composition.TransitionRuntime.RunAsync(
            new TransitionRuntimeRequest(
                WorkflowIdentity.Execute,
                new WorkflowStageIdentity(stage),
                new WorkflowTransitionIdentity(transition)));

    private static void EnqueueEvalOutput(
        FakeAgentRuntime runtime,
        string promptIdentity,
        string output)
    {
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("Canonical Runtime Context", prompt, StringComparison.Ordinal);
            Assert.Contains("Runtime Source Boundary", prompt, StringComparison.Ordinal);
            Assert.Contains("Input Product:", prompt, StringComparison.Ordinal);
            Assert.Contains("Do not inspect the repository", prompt, StringComparison.Ordinal);
            Assert.Contains(promptIdentity, prompt.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.DoesNotContain("{projectContext}", prompt, StringComparison.Ordinal);
            if (promptIdentity == "CreateNewEpic")
            {
                Assert.DoesNotContain("{newEpicProposal}", prompt, StringComparison.Ordinal);
                Assert.DoesNotContain("{epicImplementationFirstGuidance}", prompt, StringComparison.Ordinal);
                Assert.DoesNotContain("{epicAuxiliaryArtifactLimits}", prompt, StringComparison.Ordinal);
                Assert.Contains("Project context body 1.", prompt, StringComparison.Ordinal);
                Assert.Contains("# Strategic Initiative Selection", prompt, StringComparison.Ordinal);
            }
            return new AgentTurnResult(runtime.OneShotCalls.Count, AgentTurnState.Completed, output, AgentTokenUsage.Zero);
        }));
    }

    private static void EnqueueTraditionalOutput(
        FakeAgentRuntime runtime,
        string promptIdentity,
        string output)
    {
        runtime.OneShotTurns.Enqueue(new ScriptedTurn((spec, prompt, _) =>
        {
            Assert.Equal(SessionRole.OperationalExecution, spec.Role);
            Assert.Contains("Canonical Runtime Context", prompt, StringComparison.Ordinal);
            Assert.Contains(promptIdentity, prompt.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            return new AgentTurnResult(runtime.OneShotCalls.Count, AgentTurnState.Completed, output, AgentTokenUsage.Zero);
        }));
    }

    private static async Task WriteProjectContextAsync(string root)
    {
        int index = 0;
        foreach (string path in ProjectionArtifactPaths.ProjectContextSourceFiles)
        {
            index++;
            await WriteAsync(root, path, $"# Context {index}\n\nProject context body {index}.");
        }
    }

    private static async Task<(string Root, Repository Repository, FakeAgentRuntime Runtime, FakeProcessRunner Process)>
        PrepareExecuteContinuityCaseAsync(string prefix)
    {
        string root = Directory.CreateTempSubdirectory(prefix).FullName;
        await WriteAsync(root, ".agents/epic.md", ValidEvalActiveEpic());
        await WriteAsync(root, ".agents/plan.md", "# Plan\n\nCreate src/feature.cs.");
        await WriteAsync(root, ".agents/operational_context.md", "# Operational Context\n\nUse the plan.");
        await WriteAsync(root, ".agents/details.md", "# Details\n\nWrite deterministic text.");
        await WriteAsync(root, ".agents/milestones/m1.md", "# Milestone 1\n\n- [ ] Create feature.");
        await WriteProjectContextAsync(root);
        var repository = new Repository { Id = Guid.NewGuid(), Name = Path.GetFileName(root), Path = root };
        return (root, repository, new FakeAgentRuntime(new MemoryArtifactStore()), new FakeProcessRunner());
    }

    private static string ValidAdversarialProjection() =>
        """
        # Adversarial Plan Review Projection

        ## Purpose

        Test purpose.

        ## Authority Boundary

        Test authority.

        ## Projection Metadata

        | Field | Value |
        |---|---|
        | Intended Consumer | AdversarialPlanReview |

        ## Canonical Vocabulary

        | Term | Definition |
        |---|---|
        | Test | Test definition |

        ## Downstream Use Instructions

        Test downstream instructions.

        ## Projection Integrity Checklist

        - Valid.
        """;

    private static string ValidProjection(string title, string intendedConsumer) =>
        $$"""
        {{title}}

        ## Purpose

        Test purpose.

        ## Authority Boundary

        Test authority.

        ## Projection Metadata

        | Field | Value |
        |---|---|
        | Intended Consumer | {{intendedConsumer}} |

        ## Canonical Vocabulary

        | Term | Definition |
        |---|---|
        | Test | Test definition |

        ## Downstream Use Instructions

        Test downstream instructions.

        ## Projection Integrity Checklist

        - Valid.
        """;

    private static string Evaluation(string completionStatus, string drift, string recommendation) => $$"""
        # Epic Completion and Drift Evaluation

        ## Evaluation Summary

        | Field | Value |
        |---|---|
        | Epic ID | EPIC-TEST |
        | Epic Name | Test Epic |
        | Overall Completion Status | {{completionStatus}} |
        | Overall Drift Classification | {{drift}} |
        | Evidence Strength | Strong |
        | Closure Recommendation | {{recommendation}} |
        | Primary Reason | Test |
        """;

    private static string ValidEvalActiveEpic() =>
        """
        # Epic: Implement Capability

        ## Epic Metadata

        Source: eval fixture.

        ## Strategic Purpose

        Preserve the evaluated capability.

        ## Desired Capability

        The system implements the capability with observable behavior.

        ## Acceptance Criteria

        - The capability has executable validation.

        ## Milestone Roadmap

        | MilestoneID | MilestoneName | Purpose | Outcome | DependsOn | CompletionSignal |
        |---|---|---|---|---|---|
        | M1 | Implement capability | Add the behavior | Capability works | none | Tests pass |
        """;

    private static async Task WriteAdversarialReviewProductAsync(
        Repository repository,
        string content)
    {
        string relativePath = ".LoopRelay/evidence/plan/adversarial-review.md";
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        await new CanonicalWorkflowPersistenceStore(repository).UpsertProductAsync(
            new ProductRecord(
                ProductIdentity.AdversarialReview,
                WorkflowIdentity.Plan,
                new WorkflowTransitionIdentity("RunAdversarialReview"),
                [WorkflowIdentity.Plan],
                "repository-owned test evidence",
                "test",
                [relativePath],
                $"review:{content}",
                ProductFreshness.Fresh,
                ProductValidationState.Valid,
                ProductLifecycle.Active,
                [relativePath]));
    }

    private static Task WriteRepositoryChangesProductAsync(Repository repository) =>
        new CanonicalWorkflowPersistenceStore(repository).UpsertProductAsync(
            new ProductRecord(
                ProductIdentity.RepositoryChanges,
                WorkflowIdentity.Execute,
                new WorkflowTransitionIdentity("PublishRepositoryState"),
                [WorkflowIdentity.Execute],
                "repository-owned test evidence",
                "test",
                [".LoopRelay/evidence/execute-repository-state/PublishRepositoryState.md"],
                "repository-changes:test",
                ProductFreshness.Fresh,
                ProductValidationState.Valid,
                ProductLifecycle.Active,
                [".LoopRelay/evidence/execute-repository-state/PublishRepositoryState.md"]));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LoopRelay.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
