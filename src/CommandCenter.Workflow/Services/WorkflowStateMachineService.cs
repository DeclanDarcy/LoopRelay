using CommandCenter.Workflow.Abstractions;
using CommandCenter.Workflow.Models;
using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Services;

public sealed class WorkflowStateMachineService : IWorkflowStateMachineService
{
    private static readonly IReadOnlyList<WorkflowTransition> CanonicalGraph =
    [
        Transition(WorkflowStage.WorkSelection, WorkflowStage.Execution, WorkflowGateType.WorkSelection, WorkflowBlockingCondition.MissingWorkSelection, "Work must be explicitly selected before execution can start."),
        Transition(WorkflowStage.Execution, WorkflowStage.Handoff, WorkflowGateType.None, WorkflowBlockingCondition.ExecutionRunning, "Execution must finish before handoff review."),
        Transition(WorkflowStage.Handoff, WorkflowStage.Decision, WorkflowGateType.ExecutionAcceptance, WorkflowBlockingCondition.PendingHandoffAcceptance, "The execution handoff must be accepted before decision review."),
        Transition(WorkflowStage.Decision, WorkflowStage.OperationalContext, WorkflowGateType.DecisionResolution, WorkflowBlockingCondition.UnresolvedDecision, "Outstanding decisions must be resolved before operational-context review."),
        Transition(WorkflowStage.OperationalContext, WorkflowStage.Commit, WorkflowGateType.OperationalContextPromotion, WorkflowBlockingCondition.PendingContextPromotion, "Operational context must be reviewed and promoted before commit review."),
        Transition(WorkflowStage.Commit, WorkflowStage.Push, WorkflowGateType.CommitApproval, WorkflowBlockingCondition.PendingCommitApproval, "Commit approval is required before push review."),
        Transition(WorkflowStage.Push, WorkflowStage.Completed, WorkflowGateType.PushApproval, WorkflowBlockingCondition.PendingPushApproval, "Push approval is required before the workflow can complete.")
    ];

    public IReadOnlyList<WorkflowTransition> GetCanonicalGraph() => CanonicalGraph;

    public WorkflowStateMachineDiagnostics Evaluate(
        Guid repositoryId,
        WorkflowStage currentStage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        string requiredHumanAction)
    {
        IReadOnlyList<WorkflowTransitionResult> outgoing = CanonicalGraph
            .Where(transition => transition.FromStage == currentStage)
            .Select(transition => ValidateTransition(
                transition.FromStage,
                transition.ToStage,
                progressState,
                blockingGate,
                requiredHumanAction))
            .ToArray();

        List<string> reasoning = [];
        if (outgoing.Count == 0)
        {
            reasoning.Add($"Stage {currentStage} has no outgoing transition in the canonical graph.");
        }
        else
        {
            reasoning.Add($"Stage {currentStage} has {outgoing.Count} outgoing canonical transition.");
        }

        if (blockingGate is not WorkflowGateType.None)
        {
            reasoning.Add($"Blocking gate {blockingGate} prevents mechanical progression.");
        }

        IReadOnlyList<WorkflowTransitionResult> valid = outgoing
            .Where(result => result.IsValid)
            .ToArray();
        IReadOnlyList<WorkflowTransitionResult> blocked = outgoing
            .Where(result => result.IsBlocked)
            .ToArray();

        return new WorkflowStateMachineDiagnostics(
            repositoryId,
            currentStage,
            progressState,
            blockingGate,
            outgoing.Select(result => result.Transition.ToStage).Distinct().ToArray(),
            valid,
            blocked,
            reasoning,
            blocked.Select(result => result.Reason).ToArray());
    }

    public WorkflowTransitionResult ValidateTransition(
        WorkflowStage fromStage,
        WorkflowStage toStage,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate,
        string requiredHumanAction)
    {
        WorkflowTransition? transition = CanonicalGraph.FirstOrDefault(candidate =>
            candidate.FromStage == fromStage &&
            candidate.ToStage == toStage);

        if (transition is null)
        {
            var invalidTransition = new WorkflowTransition(
                fromStage,
                toStage,
                WorkflowGateType.None,
                WorkflowBlockingCondition.UnknownState,
                $"No canonical transition exists from {fromStage} to {toStage}.");

            return new WorkflowTransitionResult(
                invalidTransition,
                false,
                true,
                null,
                WorkflowBlockingCondition.UnknownState,
                invalidTransition.Description);
        }

        WorkflowBlockingCondition? condition = ResolveBlockingCondition(transition, progressState, blockingGate);
        if (condition is not null)
        {
            WorkflowGateResolution? resolution = blockingGate is WorkflowGateType.None
                ? null
                : new WorkflowGateResolution(blockingGate, condition.Value, requiredHumanAction, false);

            return new WorkflowTransitionResult(
                transition,
                false,
                true,
                resolution,
                condition,
                BuildBlockedReason(transition, condition.Value, blockingGate));
        }

        return new WorkflowTransitionResult(
            transition,
            true,
            false,
            null,
            null,
            $"Transition {fromStage} -> {toStage} is valid.");
    }

    private static WorkflowTransition Transition(
        WorkflowStage fromStage,
        WorkflowStage toStage,
        WorkflowGateType requiredGate,
        WorkflowBlockingCondition? blockingCondition,
        string description) =>
        new(fromStage, toStage, requiredGate, blockingCondition, description);

    private static WorkflowBlockingCondition? ResolveBlockingCondition(
        WorkflowTransition transition,
        WorkflowProgressState progressState,
        WorkflowGateType blockingGate)
    {
        if (progressState is WorkflowProgressState.Failed)
        {
            return WorkflowBlockingCondition.ExecutionFailure;
        }

        if (progressState is WorkflowProgressState.Blocked)
        {
            return WorkflowBlockingCondition.UnknownState;
        }

        if (transition.FromStage is WorkflowStage.Execution &&
            progressState is WorkflowProgressState.Active)
        {
            return WorkflowBlockingCondition.ExecutionRunning;
        }

        if (blockingGate is WorkflowGateType.None)
        {
            return null;
        }

        return blockingGate switch
        {
            WorkflowGateType.WorkSelection => WorkflowBlockingCondition.MissingWorkSelection,
            WorkflowGateType.ExecutionAcceptance => WorkflowBlockingCondition.PendingHandoffAcceptance,
            WorkflowGateType.DecisionResolution => WorkflowBlockingCondition.UnresolvedDecision,
            WorkflowGateType.OperationalContextReview => WorkflowBlockingCondition.PendingContextReview,
            WorkflowGateType.OperationalContextPromotion => WorkflowBlockingCondition.PendingContextPromotion,
            WorkflowGateType.CommitApproval => WorkflowBlockingCondition.PendingCommitApproval,
            WorkflowGateType.PushApproval => WorkflowBlockingCondition.PendingPushApproval,
            _ => transition.BlockingCondition ?? WorkflowBlockingCondition.UnknownState
        };
    }

    private static string BuildBlockedReason(
        WorkflowTransition transition,
        WorkflowBlockingCondition condition,
        WorkflowGateType blockingGate)
    {
        if (blockingGate is WorkflowGateType.None)
        {
            return $"Transition {transition.FromStage} -> {transition.ToStage} is blocked by {condition}.";
        }

        return $"Transition {transition.FromStage} -> {transition.ToStage} is blocked by {blockingGate}: {condition}.";
    }
}
