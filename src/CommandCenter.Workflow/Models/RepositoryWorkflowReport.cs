using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record RepositoryWorkflowReport(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    WorkflowStage CurrentStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    string RequiredHumanAction,
    int TimelineEntryCount,
    int OpenGateCount,
    int SatisfiedGateCount,
    int ContinuationEventCount,
    int PreparationEventCount,
    string HealthStatus,
    bool Certified,
    int FailedCertificationFindingCount,
    IReadOnlyList<string> Diagnostics);
