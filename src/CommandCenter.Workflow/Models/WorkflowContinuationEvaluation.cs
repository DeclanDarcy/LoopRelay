using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowContinuationEvaluation(
    Guid RepositoryId,
    WorkflowStage FromStage,
    WorkflowStage? ToStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    bool CanAdvanceMechanically,
    bool IsWaitingForHuman,
    bool IsComplete,
    string RequiredHumanAction,
    string Outcome,
    string StopReason,
    WorkflowContinuationFingerprint Fingerprint,
    WorkflowTransitionResult? Transition,
    WorkflowCompletionEvaluation Completion,
    WorkflowContinuationDiagnostics Diagnostics);
