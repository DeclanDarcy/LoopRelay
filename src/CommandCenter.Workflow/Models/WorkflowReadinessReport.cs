using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowReadinessReport(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    bool Ready,
    bool Certified,
    string HealthStatus,
    WorkflowStage CurrentStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> FailedCertificationFindings,
    IReadOnlyList<string> HealthDiagnostics);
