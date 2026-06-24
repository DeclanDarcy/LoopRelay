using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowPreparationEvaluation(
    Guid RepositoryId,
    WorkflowStage Stage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    bool CanPrepare,
    bool IsWaitingForHuman,
    bool HasDuplicateDomainEvidence,
    WorkflowPreparationCommand Command,
    string CommandName,
    string Outcome,
    string Reason,
    IReadOnlyList<string> DuplicateEvidence,
    WorkflowPreparationFingerprint Fingerprint,
    WorkflowPreparationDiagnostics Diagnostics);
