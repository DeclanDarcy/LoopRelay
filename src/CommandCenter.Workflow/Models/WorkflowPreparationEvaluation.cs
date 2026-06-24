using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowPreparationEvaluation(
    Guid RepositoryId,
    WorkflowStage Stage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    bool CanPrepare,
    bool IsWaitingForHuman,
    WorkflowPreparationCommand Command,
    string CommandName,
    string Outcome,
    string Reason,
    WorkflowPreparationFingerprint Fingerprint,
    WorkflowPreparationDiagnostics Diagnostics);
