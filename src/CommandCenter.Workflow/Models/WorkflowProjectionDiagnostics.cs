using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowProjectionDiagnostics(
    Guid RepositoryId,
    IReadOnlyList<string> ProjectionInputs,
    WorkflowStage ChosenStage,
    WorkflowGateType ChosenGate,
    IReadOnlyList<WorkflowStage> NextPossibleStages,
    IReadOnlyList<WorkflowTransitionResult> ValidTransitions,
    IReadOnlyList<WorkflowTransitionResult> BlockedTransitions,
    WorkflowStateMachineDiagnostics StateMachine,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> UnknownStates,
    IReadOnlyList<string> Conflicts);
