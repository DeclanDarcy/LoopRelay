using System.Security.Cryptography;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Persistence;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Resolution;

public sealed class WorkflowResolverTests
{
    [Fact]
    public async Task Default_invocation_selects_eval_when_eval_intent_files_exist()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evals/e1.md", "# Eval");
        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);

        Assert.Equal(WorkflowIdentity.EvalRoadmap, result.Selection.SelectedWorkflow);
        Assert.Equal("EvalRoadmapChain", result.Selection.SelectedChain);
        Assert.Contains(".agents/evals/e1.md", result.Selection.Evidence);
    }

    [Fact]
    public async Task Default_invocation_selects_traditional_when_no_eval_intent_files_exist()
    {
        string repo = CreateRepo();
        Directory.CreateDirectory(Path.Combine(repo, ".agents"));
        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);

        Assert.Equal(WorkflowIdentity.TraditionalRoadmap, result.Selection.SelectedWorkflow);
        Assert.Equal("TraditionalRoadmapChain", result.Selection.SelectedChain);
    }

    [Theory]
    [InlineData(InvocationModeKind.ForcedEvalChain, "EvalRoadmap", false)]
    [InlineData(InvocationModeKind.ForcedTraditionalChain, "TraditionalRoadmap", false)]
    [InlineData(InvocationModeKind.BoundedEval, "EvalRoadmap", true)]
    [InlineData(InvocationModeKind.BoundedTraditional, "TraditionalRoadmap", true)]
    [InlineData(InvocationModeKind.BoundedPlan, "Plan", true)]
    [InlineData(InvocationModeKind.BoundedExecute, "Execute", true)]
    public async Task Explicit_invocation_modes_override_default_selection(
        InvocationModeKind mode,
        string expectedWorkflow,
        bool bounded)
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evals/e1.md", "# Eval");
        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(mode), observation);

        Assert.Equal(expectedWorkflow, result.Selection.SelectedWorkflow.Value);
        Assert.Equal(bounded, result.Selection.IsBounded);
    }

    [Theory]
    [InlineData(WorkflowResolutionState.Active, RepositoryClassification.InProgress)]
    [InlineData(WorkflowResolutionState.Resumable, RepositoryClassification.InProgress)]
    [InlineData(WorkflowResolutionState.Waiting, RepositoryClassification.Waiting)]
    [InlineData(WorkflowResolutionState.Cancelled, RepositoryClassification.Cancelled)]
    [InlineData(WorkflowResolutionState.Failed, RepositoryClassification.Failed)]
    [InlineData(WorkflowResolutionState.Completed, RepositoryClassification.Completed)]
    [InlineData(WorkflowResolutionState.Ambiguous, RepositoryClassification.Ambiguous)]
    [InlineData(WorkflowResolutionState.Invalid, RepositoryClassification.Failed)]
    public void Workflow_state_resolution_maps_repository_classification(
        WorkflowResolutionState state,
        RepositoryClassification classification)
    {
        RepositoryObservation observation = Observation(
            workflowStates:
            [
                new ObservedWorkflowState(
                    WorkflowIdentity.Plan,
                    state,
                    state is WorkflowResolutionState.Completed or WorkflowResolutionState.Ambiguous or WorkflowResolutionState.Invalid
                        ? null
                        : new WorkflowStageIdentity("Planning"),
                    [],
                    [],
                    [$"workflow-{state}.json"]),
            ],
            products:
            [
                Observed(ProductIdentity.PreparedEpic),
                Observed(ProductIdentity.MilestoneSpecificationSet),
            ]);

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        Assert.Equal(state, result.WorkflowState);
        Assert.Equal(classification, result.Classification);
    }

    [Fact]
    public void Fresh_repository_without_observed_workflow_state_is_eligible_to_start_when_entry_gate_is_satisfied()
    {
        RepositoryObservation observation = Observation(
            products:
            [
                Observed(ProductIdentity.PreparedEpic),
                Observed(ProductIdentity.MilestoneSpecificationSet),
            ]);

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        Assert.Equal(WorkflowResolutionState.EligibleToStart, result.WorkflowState);
        Assert.Equal(RepositoryClassification.Fresh, result.Classification);
        Assert.Equal(new WorkflowStageIdentity("Planning"), result.SelectedStage);
    }

    [Fact]
    public async Task Existing_plan_artifact_infers_resumable_plan_validation_state()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/plan.md", "# Plan");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.Plan);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Plan Validation"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Planning"), state.CompletedStages);
        Assert.Contains(".agents/plan.md", state.Evidence);
        Assert.Contains("repository-observation:Plan:artifact-inferred-state", state.Evidence);

        ObservedProduct executablePlan = Assert.Single(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.ExecutablePlan);
        Assert.True(executablePlan.GateUsable);
        Assert.Equal(ProductValidationState.Unknown, executablePlan.Product.ValidationState);
        Assert.Contains(".agents/plan.md", executablePlan.Evidence);

        Assert.Equal(RepositoryClassification.InProgress, result.Classification);
        Assert.Equal(WorkflowResolutionState.Resumable, result.WorkflowState);
        Assert.Equal(new WorkflowStageIdentity("Plan Validation"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("GenerateAdversarialProjection") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Existing_partial_execution_artifacts_infer_execution_preparation_resume_state()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/plan.md", "# Plan");
        Write(repo, ".agents/operational_context.md", "# Operational Context");
        Write(repo, ".agents/specs/s1.md", "# Milestone Spec");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.Plan);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Execution Preparation"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Planning"), state.CompletedStages);
        Assert.Contains(new WorkflowStageIdentity("Plan Validation"), state.CompletedStages);

        Assert.Contains(observation.Products, product =>
            product.Product.Identity == ProductIdentity.OperationalContext &&
            product.Product.ValidationState == ProductValidationState.Unknown &&
            product.GateUsable &&
            product.Evidence.Contains(".agents/operational_context.md"));
        Assert.Equal(RepositoryClassification.InProgress, result.Classification);
        Assert.Equal(WorkflowResolutionState.Resumable, result.WorkflowState);
        Assert.Equal(new WorkflowStageIdentity("Execution Preparation"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("CollectExecutionDetails") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Existing_complete_execution_artifacts_infer_plan_workflow_completion_verification_state()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/plan.md", "# Plan");
        Write(repo, ".agents/operational_context.md", "# Operational Context");
        Write(repo, ".agents/details.md", "# Details");
        Write(repo, ".agents/milestones/m1.md", "# Milestone\n\n- [ ] Implement capability.");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.Plan);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Execution Preparation"), state.CompletedStages);
        Assert.Equal(RepositoryClassification.InProgress, result.Classification);
        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("VerifyExecuteEntryContract") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Existing_adversarial_projection_artifact_makes_review_transition_eligible()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/plan.md", "# Plan");
        Write(repo, PlanPromptContext.AdversarialPlanReviewProjectionPath, "# Adversarial Plan Review Projection");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        Assert.Contains(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.AdversarialProjection &&
                product.GateUsable &&
                product.Evidence.Contains(PlanPromptContext.AdversarialPlanReviewProjectionPath));
        Assert.Equal(new WorkflowStageIdentity("Plan Validation"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("RunAdversarialReview") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Existing_execution_milestone_files_without_checkboxes_are_invalid_products()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/milestones/m1.md", "# Milestone");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        ObservedProduct milestoneSet = Assert.Single(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.ExecutionMilestoneSet);
        Assert.False(milestoneSet.GateUsable);
        Assert.Equal(ProductValidationState.Invalid, milestoneSet.Product.ValidationState);
        Assert.Contains(".agents/milestones/m1.md", milestoneSet.Evidence);
    }

    [Fact]
    public async Task Existing_execution_milestone_files_with_all_boxes_checked_are_completion_ready_products()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/milestones/m1.md", "# Milestone\n\n- [x] Implement capability.");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        ObservedProduct milestoneSet = Assert.Single(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.ExecutionMilestoneSet);
        Assert.True(milestoneSet.GateUsable);
        Assert.Equal(ProductValidationState.Valid, milestoneSet.Product.ValidationState);
        Assert.Contains(".agents/milestones/m1.md", milestoneSet.Evidence);
    }

    [Fact]
    public async Task Existing_traditional_roadmap_products_infer_plan_entry_verification_state()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/epic.md", "# Epic");
        Write(repo, ".agents/specs/s1.md", "# Spec");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedTraditional), observation);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.TraditionalRoadmap);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Milestone Specification"), state.CompletedStages);
        Assert.Contains(".agents/epic.md", state.Evidence);
        Assert.Contains(".agents/specs/s1.md", state.Evidence);
        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("VerifyPlanEntryContract") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Existing_eval_roadmap_products_infer_plan_entry_verification_state_for_eval_selection()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evals/e1.md", "# Eval");
        Write(repo, ".agents/epic.md", "# Epic");
        Write(repo, ".agents/specs/s1.md", "# Spec");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Milestone Specification"), state.CompletedStages);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, result.Selection.SelectedWorkflow);
        Assert.Equal(new WorkflowStageIdentity("Workflow Completion"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("VerifyPlanEntryContract") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Existing_eval_dependency_inventory_infers_hypothesis_inventory_resume_state()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evals/e1.md", "# Eval");
        Write(repo, ".agents/eval-dependency-inventory.md", "# Dependency Inventory");
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Hypothesis Inventory"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Dependency Inventory"), state.CompletedStages);
        Assert.Contains(".agents/eval-dependency-inventory.md", state.Evidence);
        Assert.Contains(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.DependencyInventory &&
                product.Product.ProducerWorkflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, result.Selection.SelectedWorkflow);
        Assert.Equal(new WorkflowStageIdentity("Hypothesis Inventory"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("CreateEvalHypothesisInventory") &&
            transition.State == TransitionEligibilityState.Eligible);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Existing_eval_inventories_infer_architectural_catalog_resume_state()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evals/e1.md", "# Eval");
        Write(repo, ".agents/eval-dependency-inventory.md", "# Dependency Inventory");
        Write(repo, ".agents/eval-hypothesis-inventory.md", "# Hypothesis Inventory");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(new WorkflowStageIdentity("Architectural Catalog"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Hypothesis Inventory"), state.CompletedStages);
        Assert.Contains(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.HypothesisInventory &&
                product.Product.ProducerWorkflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(new WorkflowStageIdentity("Architectural Catalog"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("CreateEvalArchitecturalCatalog") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Existing_eval_dag_infers_next_epic_roadmap_resume_state_when_prior_products_exist()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evals/e1.md", "# Eval");
        Write(repo, ".agents/eval-dependency-inventory.md", "# Dependency Inventory");
        Write(repo, ".agents/eval-hypothesis-inventory.md", "# Hypothesis Inventory");
        Write(repo, ".agents/eval-architectural-catalog.md", "# Architectural Catalog");
        Write(repo, ".agents/eval-dag.md", "# Eval DAG");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(new WorkflowStageIdentity("Next Epic Roadmap"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Eval DAG"), state.CompletedStages);
        Assert.Contains(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.EvalDag &&
                product.Product.ProducerWorkflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(new WorkflowStageIdentity("Next Epic Roadmap"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("CreateNextEpicRoadmap") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Downstream_eval_artifact_without_prior_products_does_not_skip_to_later_stage()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evals/e1.md", "# Eval");
        Write(repo, ".agents/eval-dag.md", "# Eval DAG Without Prior Products");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);

        Assert.DoesNotContain(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.EvalRoadmap);
        Assert.Equal(WorkflowIdentity.EvalRoadmap, result.Selection.SelectedWorkflow);
        Assert.Equal(WorkflowResolutionState.EligibleToStart, result.WorkflowState);
        Assert.Equal(new WorkflowStageIdentity("Evaluation Foundation"), result.SelectedStage);
    }

    [Fact]
    public async Task Existing_execute_decision_artifact_infers_implementation_resume_state()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/decisions/decisions.md", "# Decisions");
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedExecute), observation);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.Execute);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Implementation"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Implementation Planning"), state.CompletedStages);
        Assert.Contains(".agents/decisions/decisions.md", state.Evidence);
        Assert.Contains("repository-observation:Execute:artifact-inferred-state", state.Evidence);

        ObservedProduct decisionSet = Assert.Single(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.DecisionSet);
        Assert.True(decisionSet.GateUsable);
        Assert.Equal(ProductValidationState.Unknown, decisionSet.Product.ValidationState);

        Assert.Equal(RepositoryClassification.InProgress, result.Classification);
        Assert.Equal(WorkflowResolutionState.Resumable, result.WorkflowState);
        Assert.Equal(new WorkflowStageIdentity("Implementation"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("ExecuteImplementationSlice") &&
            transition.State == TransitionEligibilityState.Eligible);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Existing_execute_handoff_artifacts_infer_continuity_resume_state()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evidence/execution/execution.0001.md", "# Execution Evidence");
        Write(repo, ".agents/handoffs/handoff.md", "# Handoff");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedExecute), observation);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.Execute);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Execution Continuity"), state.CurrentStage);
        Assert.Contains(new WorkflowStageIdentity("Implementation"), state.CompletedStages);
        Assert.Contains(".agents/evidence/execution/execution.0001.md", state.Evidence);
        Assert.Contains(".agents/handoffs/handoff.md", state.Evidence);

        Assert.Contains(observation.Products, product =>
            product.Product.Identity == ProductIdentity.ImplementationSlice &&
            product.Evidence.Contains(".agents/evidence/execution/execution.0001.md"));
        Assert.Contains(observation.Products, product =>
            product.Product.Identity == ProductIdentity.ExecutionHandoff &&
            product.Evidence.Contains(".agents/handoffs/handoff.md"));
        Assert.Equal(RepositoryClassification.InProgress, result.Classification);
        Assert.Equal(new WorkflowStageIdentity("Execution Continuity"), result.SelectedStage);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("UpdateOperationalContext") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Existing_completion_evidence_infers_completion_resume_without_closing_execute()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evidence/evaluations/epic-completion-and-drift.0001.md", "# Completion Evaluation");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedExecute), observation);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.Execute);
        Assert.Equal(WorkflowResolutionState.Resumable, state.State);
        Assert.Equal(new WorkflowStageIdentity("Completion"), state.CurrentStage);
        Assert.Contains(".agents/evidence/evaluations/epic-completion-and-drift.0001.md", state.Evidence);
        Assert.DoesNotContain(observation.Products, product => product.Product.Identity == ProductIdentity.CertifiedCompletion);

        Assert.Equal(RepositoryClassification.InProgress, result.Classification);
        Assert.Equal(WorkflowResolutionState.Resumable, result.WorkflowState);
        Assert.Equal(new WorkflowStageIdentity("Completion"), result.SelectedStage);
        Assert.NotEqual(RepositoryClassification.Completed, result.Classification);
        Assert.Contains(result.TransitionEligibility, transition =>
            transition.Transition == new WorkflowTransitionIdentity("InterpretCompletionRoute") &&
            transition.State == TransitionEligibilityState.Eligible);
    }

    [Fact]
    public async Task Execution_trust_posture_evidence_does_not_infer_execute_progress()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evidence/execution/execution-trust-posture.0001.md", "# Trust Posture");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedExecute), observation);

        Assert.DoesNotContain(observation.WorkflowStates, item => item.Workflow == WorkflowIdentity.Execute);
        Assert.DoesNotContain(observation.Products, product => product.Product.Identity == ProductIdentity.ImplementationSlice);
        Assert.DoesNotContain(observation.Products, product => product.Product.Identity == ProductIdentity.CompletionEvidence);
        Assert.NotEqual(RepositoryClassification.Completed, result.Classification);
    }

    [Fact]
    public void Transition_eligibility_uses_products_and_gate_results()
    {
        RepositoryObservation observation = Observation(
            products:
            [
                Observed(ProductIdentity.PreparedEpic),
                Observed(ProductIdentity.MilestoneSpecificationSet),
            ]);

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        TransitionEligibility writePlan = Assert.Single(
            result.TransitionEligibility,
            transition => transition.Transition == new WorkflowTransitionIdentity("WriteExecutablePlan"));
        Assert.Equal(TransitionEligibilityState.Eligible, writePlan.State);

        observation = Observation(
            workflowStates:
            [
                new ObservedWorkflowState(
                    WorkflowIdentity.Plan,
                    WorkflowResolutionState.Active,
                    new WorkflowStageIdentity("Plan Validation"),
                    [new WorkflowStageIdentity("Planning")],
                    [],
                    ["plan-validation-state.md"]),
            ],
            products:
            [
                Observed(ProductIdentity.ExecutablePlan, WorkflowIdentity.Plan),
            ]);

        result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        TransitionEligibility projection = Assert.Single(
            result.TransitionEligibility,
            transition => transition.Transition == new WorkflowTransitionIdentity("GenerateAdversarialProjection"));
        TransitionEligibility review = Assert.Single(
            result.TransitionEligibility,
            transition => transition.Transition == new WorkflowTransitionIdentity("RunAdversarialReview"));
        Assert.Equal(TransitionEligibilityState.Eligible, projection.State);
        Assert.Equal(TransitionEligibilityState.MissingRequiredInput, review.State);
        Assert.Contains(review.UnsatisfiedGates, gate => gate.Contains("AdversarialProjection", StringComparison.Ordinal));
    }

    [Fact]
    public void Artifact_existence_alone_never_implies_workflow_completion()
    {
        RepositoryObservation observation = Observation(
            products:
            [
                Observed(ProductIdentity.CertifiedCompletion, WorkflowIdentity.Execute),
            ]);

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedExecute), observation);

        Assert.NotEqual(WorkflowResolutionState.Completed, result.WorkflowState);
        Assert.NotEqual(RepositoryClassification.Completed, result.Classification);
    }

    [Fact]
    public async Task Completion_archive_record_infers_certified_execute_closure()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/archive/epics/1.md", "# Completed Epic\n\nSynthesized closure.");
        Write(repo, ".agents/archive/epics/1/archive-metadata.json", """{"SchemaVersion":"completed-epic-archive.v1"}""");
        Write(repo, ".agents/archive/epics/1/plan.md", "# Archived Plan");
        Write(repo, ".agents/archive/epics/1/milestones/m1.md", "# Archived Milestone");
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        ObservedWorkflowState state = Assert.Single(
            observation.WorkflowStates,
            item => item.Workflow == WorkflowIdentity.Execute);
        Assert.Equal(WorkflowResolutionState.Completed, state.State);
        Assert.Null(state.CurrentStage);
        Assert.Contains(".agents/archive/epics/1.md", state.Evidence);
        Assert.Contains(".agents/archive/epics/1", state.Evidence);

        ObservedProduct certifiedCompletion = Assert.Single(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.CertifiedCompletion);
        Assert.Equal(WorkflowIdentity.Execute, certifiedCompletion.Product.ProducerWorkflow);
        Assert.Equal(ProductLifecycle.Archived, certifiedCompletion.Product.Lifecycle);
        Assert.Equal(ProductValidationState.Valid, certifiedCompletion.Product.ValidationState);
        Assert.Contains(".agents/archive/epics/1/plan.md", certifiedCompletion.Product.StorageRepresentations);
        Assert.Contains(".agents/archive/epics/1/milestones/m1.md", certifiedCompletion.Product.StorageRepresentations);
        Assert.Contains(
            observation.Products,
            product => product.Product.Identity == ProductIdentity.CompletionEvidence);

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedExecute), observation);

        Assert.Equal(RepositoryClassification.Completed, result.Classification);
        Assert.Equal(WorkflowResolutionState.Completed, result.WorkflowState);
        Assert.Null(result.SelectedStage);

        RepositoryObservation secondObservation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult secondResult = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedExecute), secondObservation);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);

        Assert.Equal(before, after);
        Assert.Equal(result.WorkflowState, secondResult.WorkflowState);
        Assert.Equal(result.Classification, secondResult.Classification);
    }

    [Fact]
    public async Task Completion_synthesis_without_archive_directory_does_not_close_execute()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/archive/epics/1.md", "# Completed Epic\n\nSynthesis without retained archive record.");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedExecute), observation);

        Assert.DoesNotContain(observation.WorkflowStates, item => item.Workflow == WorkflowIdentity.Execute);
        Assert.DoesNotContain(observation.Products, product => product.Product.Identity == ProductIdentity.CertifiedCompletion);
        Assert.NotEqual(RepositoryClassification.Completed, result.Classification);
        Assert.NotEqual(WorkflowResolutionState.Completed, result.WorkflowState);
    }

    [Fact]
    public async Task Repository_observation_is_deterministic_and_non_mutating()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/evals/e1.md", "# Eval");
        Write(repo, ".agents/epic.md", "# Epic");
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        var observer = new RepositoryObserver();
        RepositoryObservation first = await observer.ObserveAsync(repo);
        WorkflowResolutionResult firstResolution = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), first);
        RepositoryObservation second = await observer.ObserveAsync(repo);
        WorkflowResolutionResult secondResolution = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), second);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);

        Assert.Equal(first.SelectionKey(), second.SelectionKey());
        Assert.Equal(firstResolution.Explanation.Decision, secondResolution.Explanation.Decision);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Repository_observation_reads_canonical_workflow_persistence()
    {
        string repo = CreateRepo();
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        var store = new CanonicalWorkflowPersistenceStore(repository);
        await store.UpsertWorkflowStateAsync(new CanonicalWorkflowStateRecord(
            WorkflowIdentity.Plan,
            WorkflowResolutionState.Active,
            new WorkflowStageIdentity("Planning"),
            RuntimeOutcomeKind.Waiting,
            DateTimeOffset.UtcNow,
            ["workflow-state.md"]));
        await store.UpsertStageStateAsync(new CanonicalStageStateRecord(
            WorkflowIdentity.Plan,
            new WorkflowStageIdentity("Planning"),
            WorkflowResolutionState.Active,
            DateTimeOffset.UtcNow,
            ["stage-state.md"]));
        await store.UpsertTransitionRunAsync(new CanonicalTransitionRunRecord(
            "run-1",
            WorkflowIdentity.Plan,
            new WorkflowStageIdentity("Planning"),
            new WorkflowTransitionIdentity("CreateExecutablePlan"),
            TransitionDurableState.Completed,
            RuntimeOutcomeKind.Completed,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "input-hash",
            "completed",
            ["transition-run.md"]));
        await store.UpsertProductAsync(new ProductRecord(
            ProductIdentity.ExecutablePlan,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity("CreateExecutablePlan"),
            [WorkflowIdentity.Execute],
            "repository-owned",
            "canonical",
            [".agents/plan.md"],
            "causal",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            ["product.md"]));

        // ExecutablePlan is filesystem-authoritative (M3): the ledger row alone no longer
        // observes the product, so the collaboration file must exist in the working tree.
        Write(repo, ".agents/plan.md", "# Plan");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        ObservedWorkflowState state = Assert.Single(observation.WorkflowStates);
        Assert.Equal(WorkflowIdentity.Plan, state.Workflow);
        Assert.Equal(new WorkflowStageIdentity("Planning"), state.CurrentStage);
        Assert.Contains(observation.Products, product => product.Product.Identity == ProductIdentity.ExecutablePlan);
        Assert.Contains(observation.TransitionRuns, run =>
            run.Transition == new WorkflowTransitionIdentity("CreateExecutablePlan") &&
            run.State == TransitionEligibilityState.Completed);
        Assert.Contains(observation.LifecycleRows, row => row.Identity == "Plan:Planning");
    }

    [Fact]
    public async Task Ledger_row_does_not_mask_a_present_collaboration_file()
    {
        string repo = CreateRepo();
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        Write(repo, ".agents/plan.md", "# Plan");
        await new CanonicalWorkflowPersistenceStore(repository).UpsertProductAsync(new ProductRecord(
            ProductIdentity.ExecutablePlan,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity("CreateExecutablePlan"),
            [WorkflowIdentity.Execute],
            "repository-owned",
            "canonical",
            [".agents/plan.md"],
            "causal",
            ProductFreshness.Stale,
            ProductValidationState.Invalid,
            ProductLifecycle.Active,
            ["product.md"]));

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        ObservedProduct observed = Assert.Single(observation.Products, product => product.Product.Identity == ProductIdentity.ExecutablePlan);
        Assert.Equal("repository observation", observed.Product.Authority);
        Assert.Equal(ProductValidationState.Unknown, observed.Product.ValidationState);
        Assert.True(observed.GateUsable);
    }

    [Fact]
    public async Task Ledger_row_does_not_substitute_for_a_missing_collaboration_file()
    {
        string repo = CreateRepo();
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        await new CanonicalWorkflowPersistenceStore(repository).UpsertProductAsync(new ProductRecord(
            ProductIdentity.ExecutablePlan,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity("CreateExecutablePlan"),
            [WorkflowIdentity.Execute],
            "repository-owned",
            "canonical",
            [".agents/plan.md"],
            "causal",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            ["product.md"]));

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        Assert.DoesNotContain(observation.Products, product => product.Product.Identity == ProductIdentity.ExecutablePlan);
    }

    [Fact]
    public async Task System_owned_product_row_remains_ledger_authoritative()
    {
        string repo = CreateRepo();
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(repo),
            Path = repo,
        };
        await new CanonicalWorkflowPersistenceStore(repository).UpsertProductAsync(new ProductRecord(
            ProductIdentity.AdversarialReview,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity("RunAdversarialReview"),
            [WorkflowIdentity.Plan],
            "repository-owned",
            "canonical",
            [".agents/projections/adversarial-plan-review.md"],
            "causal",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            ["review.md"]));

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        ObservedProduct observed = Assert.Single(observation.Products, product => product.Product.Identity == ProductIdentity.AdversarialReview);
        Assert.Equal("canonical", observed.Product.Authority);
        Assert.True(observed.GateUsable);
    }

    [Fact]
    public async Task Archived_representation_is_not_selected_over_an_active_one()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/archive/epics/1.md", "# Epic 1 synthesis");
        Write(repo, ".agents/archive/epics/1/evidence.md", "archived evidence");
        Write(repo, ".agents/evidence/evaluations/current.md", "live completion evidence");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        ObservedProduct completionEvidence = Assert.Single(observation.Products, product => product.Product.Identity == ProductIdentity.CompletionEvidence);
        Assert.Equal(ProductLifecycle.Active, completionEvidence.Product.Lifecycle);
        Assert.Equal("repository observation", completionEvidence.Product.Authority);
        ObservedProduct certifiedCompletion = Assert.Single(observation.Products, product => product.Product.Identity == ProductIdentity.CertifiedCompletion);
        Assert.Equal(ProductLifecycle.Archived, certifiedCompletion.Product.Lifecycle);
    }

    [Fact]
    public async Task Storage_observer_reports_filesystem_sqlite_mixed_missing_and_corrupt_authority()
    {
        string missing = CreateRepo();
        string filesystem = CreateRepo();
        Directory.CreateDirectory(Path.Combine(filesystem, ".agents"));
        string sqlite = CreateRepo();
        await CreateSqliteDatabaseAsync(sqlite);
        string mixed = CreateRepo();
        Directory.CreateDirectory(Path.Combine(mixed, ".agents"));
        await CreateSqliteDatabaseAsync(mixed);
        string corrupt = CreateRepo();
        WriteBytes(corrupt, ".LoopRelay/persistence/looprelay.sqlite3", "not sqlite"u8.ToArray());

        Assert.Equal(StorageAuthorityKind.Missing, (await new RepositoryObserver().ObserveAsync(missing)).StorageAuthority.Authority);
        Assert.Equal(StorageAuthorityKind.FilesystemExport, (await new RepositoryObserver().ObserveAsync(filesystem)).StorageAuthority.Authority);
        Assert.Equal(StorageAuthorityKind.CanonicalSqlite, (await new RepositoryObserver().ObserveAsync(sqlite)).StorageAuthority.Authority);
        Assert.Equal(StorageAuthorityKind.Mixed, (await new RepositoryObserver().ObserveAsync(mixed)).StorageAuthority.Authority);
        RepositoryObservation corruptObservation = await new RepositoryObserver().ObserveAsync(corrupt);
        Assert.Equal(StorageAuthorityKind.Corrupt, corruptObservation.StorageAuthority.Authority);
        Assert.False(corruptObservation.StorageAuthority.UsableAuthority);
    }

    [Fact]
    public async Task Storage_observer_blocks_unsupported_schema_without_repairing_it()
    {
        string repo = CreateRepo();
        await CreateSqliteDatabaseAsync(repo);
        await ExecuteSqlAsync(repo, "UPDATE schema_metadata SET value = '999' WHERE key = 'schema_version';");

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        Assert.Equal(StorageAuthorityKind.Unsupported, observation.StorageAuthority.Authority);
        Assert.False(observation.StorageAuthority.UsableAuthority);
        Assert.Contains(observation.StorageVerification.UnsupportedSchema, item => item.Contains("999", StringComparison.Ordinal));
        Assert.Equal("999", await ScalarStringAsync(repo, "SELECT value FROM schema_metadata WHERE key = 'schema_version';"));
    }

    [Fact]
    public async Task Storage_observer_reports_partial_workflow_transactions_as_unusable()
    {
        string repo = CreateRepo();
        await CreateSqliteDatabaseAsync(repo);
        await ExecuteSqlAsync(
            repo,
            """
            INSERT INTO workflow_transactions (
                transaction_id, workflow_name, correlation_id, status, started_at, completed_at, marker_json)
            VALUES (
                'tx-1', 'TraditionalRoadmap', 'corr-1', 'Started', '2026-07-10T12:00:00.0000000Z', NULL, '{}');
            """);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);

        Assert.False(observation.StorageAuthority.UsableAuthority);
        Assert.Contains(observation.StorageVerification.PartialTransactions, item => item.Contains("tx-1", StringComparison.Ordinal));
        Assert.Contains(observation.StorageVerification.BlockingConditions, warning => warning.Category == WarningCategory.Storage);
    }

    [Fact]
    public async Task Repository_observation_reads_legacy_decision_session_resume_state_without_importing_or_deleting()
    {
        string repo = CreateRepo();
        Write(
            repo,
            ".LoopRelay/decision-session.json",
            DecisionResumeJson("thread-legacy"));
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);

        ObservedLifecycleRow row = Assert.Single(
            observation.LifecycleRows,
            item => item.Identity == "DecisionSessionResume:LegacyFile");
        Assert.Equal("Present", row.State);
        Assert.Contains(".LoopRelay/decision-session.json", row.Evidence);
        Assert.Contains(
            observation.Evidence,
            item => item.Identity == "DecisionSessionResume:LegacyFile" &&
                item.Location == ".LoopRelay/decision-session.json");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Repository_observation_reads_sqlite_decision_session_resume_state_without_mutating_it()
    {
        string repo = CreateRepo();
        await CreateSqliteDatabaseAsync(repo);
        await ExecuteSqlAsync(
            repo,
            $$"""
            INSERT INTO decision_session_resume (id, document_json, saved_at)
            VALUES (1, '{{DecisionResumeJson("thread-sqlite")}}', '2026-07-10T12:00:00.0000000Z');
            """);
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);

        ObservedLifecycleRow row = Assert.Single(
            observation.LifecycleRows,
            item => item.Identity == "DecisionSessionResume:Sqlite");
        Assert.Equal("Present", row.State);
        Assert.Contains(".LoopRelay/persistence/looprelay.sqlite3:decision_session_resume", row.Evidence);
        Assert.Contains(
            observation.Evidence,
            item => item.Identity == "DecisionSessionResume:Sqlite" &&
                item.Location == ".LoopRelay/persistence/looprelay.sqlite3:decision_session_resume");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Repository_observation_reads_pre_unification_roadmap_filesystem_evidence_without_mutating()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/state.json", """{"SchemaVersion":"roadmap-state.v1"}""");
        Write(repo, ".agents/journal/transitions.jsonl", """{"event":"TransitionStarted"}""");
        Write(repo, ".agents/artifacts/lifecycle.json", """{"SchemaVersion":"artifact-lifecycle.v1"}""");
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);

        AssertLifecycleRow(
            observation,
            "PreUnificationRoadmapState:Filesystem",
            ".agents/state.json");
        AssertLifecycleRow(
            observation,
            "PreUnificationTransitionJournal:Filesystem",
            ".agents/journal/transitions.jsonl");
        AssertLifecycleRow(
            observation,
            "PreUnificationArtifactLifecycle:Filesystem",
            ".agents/artifacts/lifecycle.json");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Repository_observation_reports_legacy_execution_handoff_filesystem_state_as_migration_only()
    {
        string repo = CreateRepo();
        Write(repo, ".agents/state.json", """{"schemaVersion":"roadmap-state.v1","currentState":"ExecutionLoop"}""");
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);

        AssertMigrationOnlyLifecycleRow(
            observation,
            "PreUnificationExecutionHandoffState:Filesystem",
            "MigrationOnly:ExecutionLoop",
            ".agents/state.json");
        Assert.DoesNotContain(
            observation.WorkflowStates,
            state => state.Workflow == WorkflowIdentity.TraditionalRoadmap);
        Assert.Equal(WorkflowResolutionState.EligibleToStart, result.WorkflowState);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Repository_observation_reads_pre_unification_roadmap_sqlite_evidence_without_mutating()
    {
        string repo = CreateRepo();
        await CreateSqliteDatabaseAsync(repo);
        await ExecuteSqlAsync(
            repo,
            """
            INSERT INTO roadmap_state (id, document_json, updated_at)
            VALUES (1, '{"SchemaVersion":"roadmap-state.v1"}', '2026-07-10T12:00:00.0000000Z');

            INSERT INTO artifact_lifecycle (path_key, path, state, updated_at, notes)
            VALUES ('agents-epic', '.agents/epic.md', 'Ready', '2026-07-10T12:00:00.0000000Z', 'legacy');

            INSERT INTO transition_journal (
                correlation_id, event_name, recorded_at, from_state, to_state, transition,
                projection_path, prompt_contract, input_hashes_json, output_paths_json,
                duration_milliseconds, retry_count, result, decision, error, input_snapshot_json)
            VALUES (
                'corr-1', 'TransitionStarted', '2026-07-10T12:00:00.0000000Z',
                'CoreReady', 'SelectNextStrategicInitiative', 'SelectNextEpic',
                '.agents/projections/select-next-epic.md', 'SelectNextEpic',
                '{}', '[]', 0, 0, 'Started', 'None', NULL, NULL);
            """);
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);

        AssertLifecycleRow(
            observation,
            "PreUnificationRoadmapState:Sqlite",
            ".LoopRelay/persistence/looprelay.sqlite3:roadmap_state");
        AssertLifecycleRow(
            observation,
            "PreUnificationTransitionJournal:Sqlite",
            ".LoopRelay/persistence/looprelay.sqlite3:transition_journal");
        AssertLifecycleRow(
            observation,
            "PreUnificationArtifactLifecycle:Sqlite",
            ".LoopRelay/persistence/looprelay.sqlite3:artifact_lifecycle");
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Repository_observation_reports_legacy_execution_handoff_sqlite_state_as_migration_only()
    {
        string repo = CreateRepo();
        await CreateSqliteDatabaseAsync(repo);
        await ExecuteSqlAsync(
            repo,
            """
            INSERT INTO roadmap_state (id, document_json, updated_at)
            VALUES (1, '{"schemaVersion":"roadmap-state.v1","currentState":"ExecutionBlocked"}', '2026-07-10T12:00:00.0000000Z');
            """);
        IReadOnlyDictionary<string, string> before = Snapshot(repo);

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        IReadOnlyDictionary<string, string> after = Snapshot(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);

        AssertMigrationOnlyLifecycleRow(
            observation,
            "PreUnificationExecutionHandoffState:Sqlite",
            "MigrationOnly:ExecutionBlocked",
            ".LoopRelay/persistence/looprelay.sqlite3:roadmap_state");
        Assert.DoesNotContain(
            observation.WorkflowStates,
            state => state.Workflow == WorkflowIdentity.TraditionalRoadmap);
        Assert.Equal(WorkflowResolutionState.EligibleToStart, result.WorkflowState);
        Assert.Equal(before, after);
    }

    [Fact]
    public void Resolution_stops_with_storage_unusable_when_storage_verification_reports_partial_transactions()
    {
        var warning = new ResolutionWarning(
            WarningCategory.Storage,
            "partial transaction",
            "storage verifier",
            "retry or repair",
            ["workflow_transactions"]);
        RepositoryObservation observation = Observation(
            storage: new StorageVerificationResult(
                StorageAuthorityKind.CanonicalSqlite,
                UsableAuthority: false,
                StaleExports: [],
                Conflicts: [],
                Corruption: [],
                UnsupportedSchema: [],
                UnresolvedReferences: [],
                PartialTransactions: ["workflow_transactions"],
                BlockingConditions: [warning],
                Evidence: ["workflow_transactions"]));

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.DefaultChained), observation);

        Assert.Equal(RepositoryClassification.StorageUnusable, result.Classification);
        Assert.Contains(result.Explanation.Warnings, item => item.Concern == "partial transaction");
    }

    [Fact]
    public async Task Previously_latched_blocked_workflow_resolves_on_its_real_gate_condition_after_migration()
    {
        string repo = CreateRepo();
        await CreateSqliteDatabaseAsync(repo);
        await ExecuteSqlAsync(
            repo,
            """
            INSERT INTO canonical_workflow_states (workflow_identity, state, current_stage, outcome, updated_at, evidence_json)
            VALUES ('Plan', 'Blocked', 'Planning', 'Blocked', '2026-07-10T12:00:00.0000000Z', '["legacy-blocked.md"]');

            INSERT INTO canonical_stage_states (workflow_identity, stage_identity, state, updated_at, evidence_json)
            VALUES ('Plan', 'Planning', 'Blocked', '2026-07-10T12:00:00.0000000Z', '["legacy-blocked-stage.md"]');
            """);

        // The next invocation's schema pass migrates the legacy labels; no unblock command exists or is needed.
        await CreateSqliteDatabaseAsync(repo);

        Assert.Equal("Resumable", await ScalarStringAsync(repo, "SELECT state FROM canonical_workflow_states WHERE workflow_identity = 'Plan';"));
        Assert.Equal("MissingRequiredInput", await ScalarStringAsync(repo, "SELECT outcome FROM canonical_workflow_states WHERE workflow_identity = 'Plan';"));
        Assert.Equal("Resumable", await ScalarStringAsync(repo, "SELECT state FROM canonical_stage_states WHERE workflow_identity = 'Plan';"));

        RepositoryObservation observation = await new RepositoryObserver().ObserveAsync(repo);
        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        Assert.Equal(WorkflowResolutionState.Resumable, result.WorkflowState);
        Assert.Equal(RepositoryClassification.InProgress, result.Classification);
        Assert.All(
            result.TransitionEligibility,
            transition => Assert.True(
                transition.State is TransitionEligibilityState.Eligible or TransitionEligibilityState.MissingRequiredInput,
                $"Transition `{transition.Transition}` must stop on its real gate condition, got `{transition.State}`."));
        Assert.Contains(
            result.TransitionEligibility,
            transition => transition.State == TransitionEligibilityState.MissingRequiredInput);
    }

    [Fact]
    public void Resolution_reports_ambiguous_repository_when_multiple_states_exist_for_the_selected_workflow()
    {
        RepositoryObservation observation = Observation(
            workflowStates:
            [
                new ObservedWorkflowState(WorkflowIdentity.Plan, WorkflowResolutionState.Active, new WorkflowStageIdentity("Planning"), [], [], ["a.json"]),
                new ObservedWorkflowState(WorkflowIdentity.Plan, WorkflowResolutionState.Resumable, new WorkflowStageIdentity("Planning"), [], [], ["b.json"]),
            ],
            products:
            [
                Observed(ProductIdentity.PreparedEpic),
                Observed(ProductIdentity.MilestoneSpecificationSet),
            ]);

        WorkflowResolutionResult result = Resolve(new WorkflowInvocation(InvocationModeKind.BoundedPlan), observation);

        Assert.Equal(RepositoryClassification.Ambiguous, result.Classification);
        Assert.Equal(WorkflowResolutionState.Ambiguous, result.WorkflowState);
        Assert.Contains(result.Explanation.Ambiguities, ambiguity => ambiguity.Category == AmbiguityCategory.Workflow);
    }

    private static WorkflowResolutionResult Resolve(
        WorkflowInvocation invocation,
        RepositoryObservation observation) =>
        new WorkflowResolver().Resolve(
            invocation,
            observation,
            CanonicalWorkflowDefinitionSketches.CreateAll());

    private static RepositoryObservation Observation(
        IReadOnlyList<ObservedWorkflowState>? workflowStates = null,
        IReadOnlyList<ObservedProduct>? products = null,
        StorageVerificationResult? storage = null)
    {
        StorageVerificationResult effectiveStorage = storage ?? new StorageVerificationResult(
            StorageAuthorityKind.FilesystemExport,
            UsableAuthority: true,
            StaleExports: [],
            Conflicts: [],
            Corruption: [],
            UnsupportedSchema: [],
            UnresolvedReferences: [],
            PartialTransactions: [],
            BlockingConditions: [],
            Evidence: [".agents"]);
        return new RepositoryObservation(
            "repo",
            new StorageAuthoritySnapshot(
                effectiveStorage.Authority,
                effectiveStorage.UsableAuthority,
                effectiveStorage.UsableAuthority ? "test" : "unusable",
                effectiveStorage.Evidence),
            workflowStates ?? [],
            products ?? [],
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(IsRepository: true, HasWorkingTreeChanges: false, CurrentBranch: "main", Evidence: [".git"]),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: [],
            StorageVerification: effectiveStorage);
    }

    private static ObservedProduct Observed(
        ProductIdentity identity,
        WorkflowIdentity producer = default,
        ProductFreshness freshness = ProductFreshness.Fresh,
        ProductValidationState validation = ProductValidationState.Valid)
    {
        WorkflowIdentity effectiveProducer = producer.IsEmpty ? WorkflowIdentity.TraditionalRoadmap : producer;
        var record = new ProductRecord(
            identity,
            effectiveProducer,
            new WorkflowTransitionIdentity($"Produce{identity}"),
            [WorkflowIdentity.Plan],
            "repository-owned test evidence",
            "test",
            [$"{identity}.md"],
            $"hash-{identity}",
            freshness,
            validation,
            ProductLifecycle.Active,
            [$"{identity}.md"]);
        return new ObservedProduct(record, GateUsable: true, record.EvidenceLocations);
    }

    private static string CreateRepo() =>
        Directory.CreateTempSubdirectory("looprelay-resolution-").FullName;

    private static void Write(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, Normalize(relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static void WriteBytes(string root, string relativePath, byte[] content)
    {
        string path = Path.Combine(root, Normalize(relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    private static void AssertLifecycleRow(
        RepositoryObservation observation,
        string identity,
        string evidence)
    {
        ObservedLifecycleRow row = Assert.Single(
            observation.LifecycleRows,
            item => item.Identity == identity);
        Assert.Equal("Present", row.State);
        Assert.Contains(evidence, row.Evidence);
        Assert.Contains(
            observation.Evidence,
            item => item.Identity == identity && item.Location == evidence);
    }

    private static void AssertMigrationOnlyLifecycleRow(
        RepositoryObservation observation,
        string identity,
        string state,
        string evidence)
    {
        ObservedLifecycleRow row = Assert.Single(
            observation.LifecycleRows,
            item => item.Identity == identity);
        Assert.Equal(state, row.State);
        Assert.Contains(evidence, row.Evidence);
        Assert.Contains(
            observation.Evidence,
            item => item.Identity == identity && item.Location == evidence);
    }

    private static string DecisionResumeJson(string threadId) =>
        $$"""
        {"threadId":"{{threadId}}","occupancyTokens":100,"reuseCost":5,"reuseCycles":2,"lastCycleCost":3,"prevCycleCost":2,"transferCost":300000,"transferCount":1,"previousOperationalContextSize":500,"operationalContextGrowthStreak":1,"schemaVersion":1,"savedAtUtc":"2026-07-10T12:00:00.0000000Z"}
        """;

    private static IReadOnlyDictionary<string, string> Snapshot(string root) =>
        Directory
            .EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => (Relative: Path.GetRelativePath(root, path).Replace('\\', '/'), Hash: Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()))
            .OrderBy(item => item.Relative, StringComparer.Ordinal)
            .ToDictionary(item => item.Relative, item => item.Hash, StringComparer.Ordinal);

    private static async Task CreateSqliteDatabaseAsync(string root)
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(root),
            Path = root,
        };
        string databasePath = LoopRelayWorkspaceDatabase.Resolve(repository);
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        await using Microsoft.Data.Sqlite.SqliteConnection connection =
            LoopRelayWorkspaceDatabase.OpenReadWriteCreate(databasePath);
        await connection.OpenAsync();
        await LoopRelayWorkspaceDatabase.EnsureSchemaAsync(connection);
    }

    private static async Task ExecuteSqlAsync(string root, string commandText)
    {
        await using Microsoft.Data.Sqlite.SqliteConnection connection =
            LoopRelayWorkspaceDatabase.OpenReadWrite(DatabasePath(root));
        await connection.OpenAsync();
        await using Microsoft.Data.Sqlite.SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarStringAsync(string root, string commandText)
    {
        await using Microsoft.Data.Sqlite.SqliteConnection connection =
            LoopRelayWorkspaceDatabase.OpenReadOnly(DatabasePath(root));
        await connection.OpenAsync();
        await using Microsoft.Data.Sqlite.SqliteCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? scalar = await command.ExecuteScalarAsync();
        return scalar is null or DBNull ? null : Convert.ToString(scalar);
    }

    private static string DatabasePath(string root)
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(root),
            Path = root,
        };
        return LoopRelayWorkspaceDatabase.Resolve(repository);
    }

    private static string Normalize(string path) =>
        path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
}

internal static class RepositoryObservationTestExtensions
{
    public static string SelectionKey(this RepositoryObservation observation) =>
        $"{observation.StorageAuthority.Authority}:{string.Join(",", observation.EvaluationIntentPaths)}:{string.Join(",", observation.Products.Select(product => product.Product.Identity.Value))}";
}
