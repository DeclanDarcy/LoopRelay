using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowInstance(
    Guid RepositoryId,
    WorkflowStage CurrentStage,
    WorkflowProgressState ProgressState,
    WorkflowGateType BlockingGate,
    string RequiredHumanAction,
    IReadOnlyList<WorkflowTimelineEntry> Timeline,
    WorkflowProjectionDiagnostics Diagnostics);
