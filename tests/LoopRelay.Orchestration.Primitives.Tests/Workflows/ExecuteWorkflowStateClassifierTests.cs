using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Workflows;

public sealed class ExecuteWorkflowStateClassifierTests
{
    [Fact]
    public void Classify_distinguishes_execute_progress_states_from_products_and_workflow_state()
    {
        AssertState(ExecuteWorkflowStateKind.NotStarted, Observation());
        AssertState(
            ExecuteWorkflowStateKind.ReadinessInProgress,
            Observation(workflow: Workflow(WorkflowResolutionState.Active, "Execution Readiness")));
        AssertState(
            ExecuteWorkflowStateKind.DecisionPlanningInProgress,
            Observation(products: [Product(ProductIdentity.ExecutionReadiness)]));
        AssertState(
            ExecuteWorkflowStateKind.ImplementationInProgress,
            Observation(products: [Product(ProductIdentity.DecisionSet)]));
        AssertState(
            ExecuteWorkflowStateKind.ExecutionContinuityInProgress,
            Observation(products: [Product(ProductIdentity.ImplementationSlice)]));
        AssertState(
            ExecuteWorkflowStateKind.ExecutionContinuityInProgress,
            Observation(products: [Product(ProductIdentity.ExecutionHandoff)]));
        AssertState(
            ExecuteWorkflowStateKind.CompletionInProgress,
            Observation(products: [Product(ProductIdentity.CompletionEvidence)]));
        AssertState(
            ExecuteWorkflowStateKind.WorkflowCompletionInProgress,
            Observation(products: [Product(ProductIdentity.CompletionRoute)]));
        AssertState(
            ExecuteWorkflowStateKind.Waiting,
            Observation(workflow: Workflow(WorkflowResolutionState.Waiting, "Completion")));
        AssertState(
            ExecuteWorkflowStateKind.ImplementationInProgress,
            Observation(workflow: Workflow(WorkflowResolutionState.Resumable, "Implementation")));
        AssertState(
            ExecuteWorkflowStateKind.Cancelled,
            Observation(workflow: Workflow(WorkflowResolutionState.Cancelled, "Implementation")));
        AssertState(
            ExecuteWorkflowStateKind.Failed,
            Observation(workflow: Workflow(WorkflowResolutionState.Failed, "Implementation")));
        AssertState(
            ExecuteWorkflowStateKind.Completed,
            Observation(workflow: Workflow(WorkflowResolutionState.Completed, null)));
    }

    private static void AssertState(
        ExecuteWorkflowStateKind expected,
        RepositoryObservation observation)
    {
        ExecuteWorkflowState state = ExecuteWorkflowStateClassifier.Classify(observation);
        Assert.Equal(expected, state.Kind);
    }

    private static ObservedWorkflowState Workflow(
        WorkflowResolutionState state,
        string? stage) =>
        new(
            WorkflowIdentity.Execute,
            state,
            stage is null ? null : new WorkflowStageIdentity(stage),
            [],
            [],
            [$"execute-{state}.md"]);

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
            WorkflowIdentity.Execute,
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
