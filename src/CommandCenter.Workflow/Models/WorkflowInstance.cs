using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowInstance(
    Guid RepositoryId,
    WorkflowStage CurrentStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    string RequiredHumanAction,
    WorkflowExecutionProjection CurrentExecution,
    WorkflowExecutionStatus ExecutionStatus,
    bool IsExecutionEligible,
    WorkflowExecutionFailure? ExecutionFailure,
    WorkflowExecutionDiagnostics ExecutionDiagnostics,
    WorkflowHandoffProjection CurrentHandoff,
    WorkflowHandoffStatus HandoffStatus,
    WorkflowHandoffValidation HandoffValidation,
    WorkflowHandoffDiagnostics HandoffDiagnostics,
    IReadOnlyList<WorkflowStage> NextPossibleStages,
    IReadOnlyList<WorkflowTransitionResult> ValidTransitions,
    IReadOnlyList<WorkflowTransitionResult> BlockedTransitions,
    IReadOnlyList<WorkflowTimelineEntry> Timeline,
    IReadOnlyList<WorkflowGate> OpenGates,
    IReadOnlyList<WorkflowGate> SatisfiedGates,
    IReadOnlyList<WorkflowGate> GateHistory,
    WorkflowGateDiagnostics GateDiagnostics,
    WorkflowProjectionDiagnostics Diagnostics);
