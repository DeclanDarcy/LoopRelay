using LoopRelay.Orchestration.Resolution;

namespace LoopRelay.Orchestration.Workflows;

public enum PlanWorkflowStateKind
{
    NotStarted,
    PlanningInProgress,
    PlanAuthored,
    ValidationInProgress,
    ValidationComplete,
    ExecutionPreparationInProgress,
    ExecutionPreparationComplete,
    PartialExecutionProducts,
    ExecutionReady,
    Blocked,
    Cancelled,
    Failed,
    Completed,
}

public sealed record PlanWorkflowState(
    PlanWorkflowStateKind Kind,
    WorkflowStageIdentity? CurrentStage,
    IReadOnlyList<ProductIdentity> Products,
    IReadOnlyList<string> Evidence);

public static class PlanWorkflowStateClassifier
{
    public static PlanWorkflowState Classify(RepositoryObservation observation)
    {
        ObservedWorkflowState? workflow = observation.WorkflowStates
            .FirstOrDefault(state => state.Workflow == WorkflowIdentity.Plan);
        if (workflow?.State is WorkflowResolutionState.Completed)
        {
            return State(PlanWorkflowStateKind.Completed, workflow, observation);
        }

        if (workflow?.State is WorkflowResolutionState.Blocked)
        {
            return State(PlanWorkflowStateKind.Blocked, workflow, observation);
        }

        if (workflow?.State is WorkflowResolutionState.Cancelled)
        {
            return State(PlanWorkflowStateKind.Cancelled, workflow, observation);
        }

        if (workflow?.State is WorkflowResolutionState.Failed or WorkflowResolutionState.Invalid)
        {
            return State(PlanWorkflowStateKind.Failed, workflow, observation);
        }

        bool hasExecutablePlan = HasUsableProduct(observation, ProductIdentity.ExecutablePlan);
        bool hasAdversarialProjection = HasUsableProduct(observation, ProductIdentity.AdversarialProjection);
        bool hasAdversarialReview = HasUsableProduct(observation, ProductIdentity.AdversarialReview);
        bool hasOperationalContext = HasUsableProduct(observation, ProductIdentity.OperationalContext);
        bool hasExecutionDetails = HasUsableProduct(observation, ProductIdentity.ExecutionDetails);
        bool hasExecutionMilestones = HasUsableProduct(observation, ProductIdentity.ExecutionMilestoneSet);
        bool hasExecutionReadiness = HasUsableProduct(observation, ProductIdentity.ExecutionReadiness);
        int executionProducts =
            Convert.ToInt32(hasOperationalContext) +
            Convert.ToInt32(hasExecutionDetails) +
            Convert.ToInt32(hasExecutionMilestones);

        if (hasExecutablePlan &&
            hasOperationalContext &&
            hasExecutionDetails &&
            hasExecutionMilestones &&
            hasExecutionReadiness)
        {
            return State(PlanWorkflowStateKind.ExecutionReady, workflow, observation);
        }

        if (executionProducts is > 0 and < 3)
        {
            return State(PlanWorkflowStateKind.PartialExecutionProducts, workflow, observation);
        }

        if (hasExecutablePlan && executionProducts == 3)
        {
            return State(PlanWorkflowStateKind.ExecutionPreparationComplete, workflow, observation);
        }

        if (IsStage(workflow, "Execution Preparation"))
        {
            return State(PlanWorkflowStateKind.ExecutionPreparationInProgress, workflow, observation);
        }

        if (hasExecutablePlan && hasAdversarialReview)
        {
            return State(PlanWorkflowStateKind.ValidationComplete, workflow, observation);
        }

        if (IsStage(workflow, "Plan Validation") ||
            hasAdversarialProjection ||
            hasAdversarialReview)
        {
            return State(PlanWorkflowStateKind.ValidationInProgress, workflow, observation);
        }

        if (hasExecutablePlan)
        {
            return State(PlanWorkflowStateKind.PlanAuthored, workflow, observation);
        }

        if (IsStage(workflow, "Planning") ||
            workflow?.State is WorkflowResolutionState.Active or WorkflowResolutionState.Resumable)
        {
            return State(PlanWorkflowStateKind.PlanningInProgress, workflow, observation);
        }

        return State(PlanWorkflowStateKind.NotStarted, workflow, observation);
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

    private static PlanWorkflowState State(
        PlanWorkflowStateKind kind,
        ObservedWorkflowState? workflow,
        RepositoryObservation observation) =>
        new(
            kind,
            workflow?.CurrentStage,
            observation.Products
                .Where(product => product.Product.ProducerWorkflow == WorkflowIdentity.Plan ||
                    product.Product.IntendedConsumers.Contains(WorkflowIdentity.Execute))
                .Select(product => product.Product.Identity)
                .Distinct()
                .ToArray(),
            (workflow?.Evidence ?? [])
                .Concat(observation.Products.SelectMany(product => product.Evidence))
                .Distinct(StringComparer.Ordinal)
                .ToArray());
}
