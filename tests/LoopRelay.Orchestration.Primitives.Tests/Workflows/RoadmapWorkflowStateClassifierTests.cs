using LoopRelay.Orchestration.Resolution;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Workflows;

public sealed class RoadmapWorkflowStateClassifierTests
{
    [Fact]
    public void Classify_distinguishes_roadmap_progress_states_from_products_and_workflow_state()
    {
        AssertState(RoadmapWorkflowStateKind.NotStarted, Observation());
        AssertState(
            RoadmapWorkflowStateKind.EpicPreparationInProgress,
            Observation(workflow: Workflow(WorkflowResolutionState.Active, "Epic Preparation")));
        AssertState(
            RoadmapWorkflowStateKind.MilestoneSpecificationInProgress,
            Observation(products: [Product(ProductIdentity.PreparedEpic)]));
        AssertState(
            RoadmapWorkflowStateKind.PlanEntryVerificationInProgress,
            Observation(products:
            [
                Product(ProductIdentity.PreparedEpic),
                Product(ProductIdentity.MilestoneSpecificationSet),
            ]));
        AssertState(
            RoadmapWorkflowStateKind.EpicPreparationInProgress,
            Observation(workflow: Workflow(WorkflowResolutionState.Resumable, "Epic Preparation")));
        AssertState(
            RoadmapWorkflowStateKind.Cancelled,
            Observation(workflow: Workflow(WorkflowResolutionState.Cancelled, "Epic Preparation")));
        AssertState(
            RoadmapWorkflowStateKind.Failed,
            Observation(workflow: Workflow(WorkflowResolutionState.Failed, "Epic Preparation")));
        AssertState(
            RoadmapWorkflowStateKind.Completed,
            Observation(workflow: Workflow(WorkflowResolutionState.Completed, null)));
    }

    private static void AssertState(
        RoadmapWorkflowStateKind expected,
        RepositoryObservation observation)
    {
        RoadmapWorkflowState state = RoadmapWorkflowStateClassifier.Classify(
            observation,
            WorkflowIdentity.TraditionalRoadmap);
        Assert.Equal(expected, state.Kind);
    }

    private static ObservedWorkflowState Workflow(
        WorkflowResolutionState state,
        string? stage) =>
        new(
            WorkflowIdentity.TraditionalRoadmap,
            state,
            stage is null ? null : new WorkflowStageIdentity(stage),
            [],
            [],
            [$"roadmap-{state}.md"]);

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
            WorkflowIdentity.TraditionalRoadmap,
            new WorkflowTransitionIdentity($"Produce{identity}"),
            [WorkflowIdentity.Plan],
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
