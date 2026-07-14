using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Workflows;

public sealed class PlanWorkflowStateClassifierTests
{
    [Fact]
    public void Classify_distinguishes_plan_progress_states_from_products_and_workflow_state()
    {
        AssertState(PlanWorkflowStateKind.NotStarted, Observation());
        AssertState(
            PlanWorkflowStateKind.PlanningInProgress,
            Observation(workflow: Workflow(WorkflowResolutionState.Active, "Planning")));
        AssertState(
            PlanWorkflowStateKind.PlanAuthored,
            Observation(products: [Product(ProductIdentity.ExecutablePlan)]));
        AssertState(
            PlanWorkflowStateKind.ValidationInProgress,
            Observation(products: [Product(ProductIdentity.ExecutablePlan), Product(ProductIdentity.AdversarialProjection)]));
        AssertState(
            PlanWorkflowStateKind.ValidationComplete,
            Observation(products: [Product(ProductIdentity.ExecutablePlan), Product(ProductIdentity.AdversarialReview)]));
        AssertState(
            PlanWorkflowStateKind.ExecutionPreparationInProgress,
            Observation(workflow: Workflow(WorkflowResolutionState.Active, "Execution Preparation")));
        AssertState(
            PlanWorkflowStateKind.PartialExecutionProducts,
            Observation(products:
            [
                Product(ProductIdentity.ExecutablePlan),
                Product(ProductIdentity.OperationalContext),
            ]));
        AssertState(
            PlanWorkflowStateKind.ExecutionPreparationComplete,
            Observation(products:
            [
                Product(ProductIdentity.ExecutablePlan),
                Product(ProductIdentity.OperationalContext),
                Product(ProductIdentity.ExecutionDetails),
                Product(ProductIdentity.ExecutionMilestoneSet),
            ]));
        AssertState(
            PlanWorkflowStateKind.ExecutionReady,
            Observation(products:
            [
                Product(ProductIdentity.ExecutablePlan),
                Product(ProductIdentity.OperationalContext),
                Product(ProductIdentity.ExecutionDetails),
                Product(ProductIdentity.ExecutionMilestoneSet),
                Product(ProductIdentity.ExecutionReadiness),
            ]));
        AssertState(
            PlanWorkflowStateKind.PlanningInProgress,
            Observation(workflow: Workflow(WorkflowResolutionState.Resumable, "Planning")));
        AssertState(
            PlanWorkflowStateKind.Cancelled,
            Observation(workflow: Workflow(WorkflowResolutionState.Cancelled, "Planning")));
        AssertState(
            PlanWorkflowStateKind.Failed,
            Observation(workflow: Workflow(WorkflowResolutionState.Failed, "Planning")));
        AssertState(
            PlanWorkflowStateKind.Completed,
            Observation(workflow: Workflow(WorkflowResolutionState.Completed, null)));
    }

    private static void AssertState(
        PlanWorkflowStateKind expected,
        RepositoryObservation observation)
    {
        PlanWorkflowState state = PlanWorkflowStateClassifier.Classify(observation);
        Assert.Equal(expected, state.Kind);
    }

    private static ObservedWorkflowState Workflow(
        WorkflowResolutionState state,
        string? stage) =>
        new(
            WorkflowIdentity.Plan,
            state,
            stage is null ? null : new WorkflowStageIdentity(stage),
            [],
            [],
            [$"plan-{state}.md"]);

    private static RepositoryObservation Observation(
        ObservedWorkflowState? workflow = null,
        IReadOnlyList<ObservedProduct>? products = null)
    {
        var storage = new StorageVerificationResult(
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
            new StorageAuthoritySnapshot(storage.Authority, storage.UsableAuthority, "test", storage.Evidence),
            workflow is null ? [] : [workflow],
            products ?? [],
            LifecycleRows: [],
            Evidence: [],
            TransitionRuns: [],
            GitFacts: new ObservedGitFacts(IsRepository: true, HasWorkingTreeChanges: false, CurrentBranch: "main", Evidence: [".git"]),
            HumanInteractionRequirements: [],
            EvaluationIntentPaths: [],
            StorageVerification: storage);
    }

    private static ObservedProduct Product(ProductIdentity identity)
    {
        var record = new ProductRecord(
            identity,
            WorkflowIdentity.Plan,
            new WorkflowTransitionIdentity($"Produce{identity}"),
            [WorkflowIdentity.Execute],
            "repository-owned test evidence",
            "test",
            [$"{identity}.md"],
            $"hash-{identity}",
            ProductFreshness.Fresh,
            ProductValidationState.Valid,
            ProductLifecycle.Active,
            [$"{identity}.md"]);
        return new ObservedProduct(record, GateUsable: true, record.EvidenceLocations);
    }
}
