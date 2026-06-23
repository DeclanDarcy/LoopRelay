using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowProjectionDiagnostics(
    Guid RepositoryId,
    IReadOnlyList<string> ProjectionInputs,
    WorkflowStage ChosenStage,
    WorkflowGateType ChosenGate,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> UnknownStates,
    IReadOnlyList<string> Conflicts);
