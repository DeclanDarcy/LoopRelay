using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Orchestration.Workflows;

public enum ExecuteWorkflowStateKind
{
    NotStarted,
    ReadinessInProgress,
    DecisionPlanningInProgress,
    ImplementationInProgress,
    ExecutionContinuityInProgress,
    CompletionInProgress,
    WorkflowCompletionInProgress,
    Waiting,
    Cancelled,
    Failed,
    Completed,
}

public sealed record ExecuteWorkflowState(
    ExecuteWorkflowStateKind Kind,
    WorkflowStageIdentity? CurrentStage,
    IReadOnlyList<ProductIdentity> Products,
    IReadOnlyList<string> Evidence);

public static class ExecuteWorkflowStateClassifier
{
    public static ExecuteWorkflowState Classify(RepositoryObservation observation)
    {
        ObservedWorkflowState? workflow = observation.WorkflowStates
            .FirstOrDefault(state => state.Workflow == WorkflowIdentity.Execute);
        if (workflow?.State is WorkflowResolutionState.Completed)
        {
            return State(ExecuteWorkflowStateKind.Completed, workflow, observation);
        }

        if (workflow?.State is WorkflowResolutionState.Waiting)
        {
            return State(ExecuteWorkflowStateKind.Waiting, workflow, observation);
        }

        if (workflow?.State is WorkflowResolutionState.Cancelled)
        {
            return State(ExecuteWorkflowStateKind.Cancelled, workflow, observation);
        }

        if (workflow?.State is WorkflowResolutionState.Failed or WorkflowResolutionState.Invalid)
        {
            return State(ExecuteWorkflowStateKind.Failed, workflow, observation);
        }

        if (HasUsableProduct(observation, ProductIdentity.CertifiedCompletion) ||
            HasUsableProduct(observation, ProductIdentity.CompletionRoute) ||
            IsStage(workflow, "Workflow Completion"))
        {
            return State(ExecuteWorkflowStateKind.WorkflowCompletionInProgress, workflow, observation);
        }

        if (HasUsableProduct(observation, ProductIdentity.CompletionEvidence) ||
            IsStage(workflow, "Completion"))
        {
            return State(ExecuteWorkflowStateKind.CompletionInProgress, workflow, observation);
        }

        if (HasUsableProduct(observation, ProductIdentity.ExecutionHandoff) ||
            HasUsableProduct(observation, ProductIdentity.OperationalDelta) ||
            HasUsableProduct(observation, ProductIdentity.ImplementationSlice) ||
            HasUsableProduct(observation, ProductIdentity.RepositoryChanges) ||
            IsStage(workflow, "Execution Continuity"))
        {
            return State(ExecuteWorkflowStateKind.ExecutionContinuityInProgress, workflow, observation);
        }

        if (HasUsableProduct(observation, ProductIdentity.DecisionSet) ||
            IsStage(workflow, "Implementation"))
        {
            return State(ExecuteWorkflowStateKind.ImplementationInProgress, workflow, observation);
        }

        if (HasUsableProduct(observation, ProductIdentity.ExecutionReadiness) ||
            IsStage(workflow, "Implementation Planning"))
        {
            return State(ExecuteWorkflowStateKind.DecisionPlanningInProgress, workflow, observation);
        }

        if (IsStage(workflow, "Execution Readiness") ||
            workflow?.State is WorkflowResolutionState.Active or WorkflowResolutionState.Resumable)
        {
            return State(ExecuteWorkflowStateKind.ReadinessInProgress, workflow, observation);
        }

        return State(ExecuteWorkflowStateKind.NotStarted, workflow, observation);
    }

    private static bool HasUsableProduct(
        RepositoryObservation observation,
        ProductIdentity product) =>
        observation.Products.Any(observed =>
            observed.Product.Identity == product &&
            observed.GateUsable &&
            observed.Product.ValidationState is ProductValidationState.Valid or ProductValidationState.Unknown);

    private static bool IsStage(ObservedWorkflowState? workflow, string stage) =>
        workflow?.CurrentStage == new WorkflowStageIdentity(stage);

    private static ExecuteWorkflowState State(
        ExecuteWorkflowStateKind kind,
        ObservedWorkflowState? workflow,
        RepositoryObservation observation) =>
        new(
            kind,
            workflow?.CurrentStage,
            observation.Products
                .Where(product => product.Product.ProducerWorkflow == WorkflowIdentity.Execute ||
                    product.Product.IntendedConsumers.Contains(WorkflowIdentity.Execute))
                .Select(product => product.Product.Identity)
                .Distinct()
                .ToArray(),
            (workflow?.Evidence ?? [])
                .Concat(observation.Products.SelectMany(product => product.Evidence))
                .Distinct(StringComparer.Ordinal)
                .ToArray());
}
