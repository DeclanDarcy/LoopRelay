using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowCertificationResult(
    string Id,
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    string InputFingerprint,
    bool Certified,
    WorkflowStage CurrentStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    int PassedFindingCount,
    int FailedFindingCount,
    IReadOnlyList<WorkflowCertificationFinding> Findings,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Diagnostics);
