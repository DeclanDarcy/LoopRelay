using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Orchestration.Workflows;

public enum RoadmapWorkflowStateKind
{
    NotStarted,
    EpicPreparationInProgress,
    HypothesisInventoryInProgress,
    ArchitecturalCatalogInProgress,
    EvalDagInProgress,
    NextEpicRoadmapInProgress,
    ActiveEpicPreparationInProgress,
    MilestoneSpecificationInProgress,
    PlanEntryVerificationInProgress,
    Blocked,
    Cancelled,
    Failed,
    Completed,
}

public sealed record RoadmapWorkflowState(
    RoadmapWorkflowStateKind Kind,
    WorkflowStageIdentity? CurrentStage,
    IReadOnlyList<ProductIdentity> Products,
    IReadOnlyList<string> Evidence);

public static class RoadmapWorkflowStateClassifier
{
    public static RoadmapWorkflowState Classify(
        RepositoryObservation observation,
        WorkflowIdentity workflow)
    {
        ObservedWorkflowState? workflowState = observation.WorkflowStates
            .FirstOrDefault(state => state.Workflow == workflow);
        if (workflowState?.State is WorkflowResolutionState.Completed)
        {
            return State(RoadmapWorkflowStateKind.Completed, workflowState, observation);
        }

        if (workflowState?.State is WorkflowResolutionState.Blocked)
        {
            return State(RoadmapWorkflowStateKind.Blocked, workflowState, observation);
        }

        if (workflowState?.State is WorkflowResolutionState.Cancelled)
        {
            return State(RoadmapWorkflowStateKind.Cancelled, workflowState, observation);
        }

        if (workflowState?.State is WorkflowResolutionState.Failed or WorkflowResolutionState.Invalid)
        {
            return State(RoadmapWorkflowStateKind.Failed, workflowState, observation);
        }

        if (workflow == WorkflowIdentity.EvalRoadmap)
        {
            return ClassifyEvalRoadmap(observation, workflowState);
        }

        bool hasPreparedEpic = HasUsableProduct(observation, ProductIdentity.PreparedEpic);
        bool hasMilestoneSpecifications = HasUsableProduct(observation, ProductIdentity.MilestoneSpecificationSet);
        if (hasPreparedEpic && hasMilestoneSpecifications)
        {
            return State(RoadmapWorkflowStateKind.PlanEntryVerificationInProgress, workflowState, observation);
        }

        if (hasPreparedEpic)
        {
            return State(RoadmapWorkflowStateKind.MilestoneSpecificationInProgress, workflowState, observation);
        }

        if (workflowState?.State is WorkflowResolutionState.Active or WorkflowResolutionState.Resumable)
        {
            return State(RoadmapWorkflowStateKind.EpicPreparationInProgress, workflowState, observation);
        }

        return State(RoadmapWorkflowStateKind.NotStarted, workflowState, observation);
    }

    private static RoadmapWorkflowState ClassifyEvalRoadmap(
        RepositoryObservation observation,
        ObservedWorkflowState? workflowState)
    {
        bool hasPreparedEpic = HasUsableProduct(observation, ProductIdentity.PreparedEpic);
        bool hasMilestoneSpecifications = HasUsableProduct(observation, ProductIdentity.MilestoneSpecificationSet);
        if (hasPreparedEpic && hasMilestoneSpecifications)
        {
            return State(RoadmapWorkflowStateKind.PlanEntryVerificationInProgress, workflowState, observation);
        }

        if (hasPreparedEpic)
        {
            return State(RoadmapWorkflowStateKind.MilestoneSpecificationInProgress, workflowState, observation);
        }

        bool hasDependencyInventory = HasUsableProduct(observation, ProductIdentity.DependencyInventory);
        bool hasHypothesisInventory = HasUsableProduct(observation, ProductIdentity.HypothesisInventory);
        bool hasArchitecturalCatalog = HasUsableProduct(observation, ProductIdentity.ArchitecturalCatalog);
        bool hasEvalDag = HasUsableProduct(observation, ProductIdentity.EvalDag);
        bool hasNextEpicRoadmap = HasUsableProduct(observation, ProductIdentity.NextEpicRoadmap);

        if (hasDependencyInventory &&
            hasHypothesisInventory &&
            hasArchitecturalCatalog &&
            hasEvalDag &&
            hasNextEpicRoadmap)
        {
            return State(RoadmapWorkflowStateKind.ActiveEpicPreparationInProgress, workflowState, observation);
        }

        if (hasDependencyInventory &&
            hasHypothesisInventory &&
            hasArchitecturalCatalog &&
            hasEvalDag)
        {
            return State(RoadmapWorkflowStateKind.NextEpicRoadmapInProgress, workflowState, observation);
        }

        if (hasDependencyInventory &&
            hasHypothesisInventory &&
            hasArchitecturalCatalog)
        {
            return State(RoadmapWorkflowStateKind.EvalDagInProgress, workflowState, observation);
        }

        if (hasDependencyInventory && hasHypothesisInventory)
        {
            return State(RoadmapWorkflowStateKind.ArchitecturalCatalogInProgress, workflowState, observation);
        }

        if (hasDependencyInventory)
        {
            return State(RoadmapWorkflowStateKind.HypothesisInventoryInProgress, workflowState, observation);
        }

        if (workflowState?.State is WorkflowResolutionState.Active or WorkflowResolutionState.Resumable)
        {
            return State(RoadmapWorkflowStateKind.EpicPreparationInProgress, workflowState, observation);
        }

        return State(RoadmapWorkflowStateKind.NotStarted, workflowState, observation);
    }

    private static bool HasUsableProduct(
        RepositoryObservation observation,
        ProductIdentity product) =>
        observation.Products.Any(observed =>
            observed.Product.Identity == product &&
            observed.GateUsable &&
            observed.Product.ValidationState is ProductValidationState.Valid or ProductValidationState.Unknown);

    private static RoadmapWorkflowState State(
        RoadmapWorkflowStateKind kind,
        ObservedWorkflowState? workflow,
        RepositoryObservation observation) =>
        new(
            kind,
            workflow?.CurrentStage,
            observation.Products
                .Where(product => RoadmapProductIdentities.Contains(product.Product.Identity))
                .Select(product => product.Product.Identity)
                .Distinct()
                .ToArray(),
            (workflow?.Evidence ?? [])
                .Concat(observation.Products
                    .Where(product => RoadmapProductIdentities.Contains(product.Product.Identity))
                    .SelectMany(product => product.Evidence))
                .Distinct(StringComparer.Ordinal)
                .ToArray());

    private static readonly HashSet<ProductIdentity> RoadmapProductIdentities =
    [
        ProductIdentity.EvaluationIntent,
        ProductIdentity.DependencyInventory,
        ProductIdentity.HypothesisInventory,
        ProductIdentity.ArchitecturalCatalog,
        ProductIdentity.EvalDag,
        ProductIdentity.NextEpicRoadmap,
        ProductIdentity.PreparedEpic,
        ProductIdentity.MilestoneSpecificationSet,
    ];
}
