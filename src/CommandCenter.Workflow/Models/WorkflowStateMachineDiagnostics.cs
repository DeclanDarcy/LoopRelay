using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowStateMachineDiagnostics(
    Guid RepositoryId,
    WorkflowStage CurrentStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    IReadOnlyList<WorkflowStage> CandidateStages,
    IReadOnlyList<WorkflowTransitionResult> ValidTransitions,
    IReadOnlyList<WorkflowTransitionResult> BlockedTransitions,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> RejectedTransitions);
