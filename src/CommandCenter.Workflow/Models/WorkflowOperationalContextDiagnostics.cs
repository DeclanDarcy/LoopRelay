namespace CommandCenter.Workflow.Models;

public sealed record WorkflowOperationalContextDiagnostics(
    Guid RepositoryId,
    IReadOnlyList<string> ProjectionInputs,
    IReadOnlyList<string> Reasoning,
    IReadOnlyList<string> ReviewSignals,
    IReadOnlyList<string> PromotionSignals,
    IReadOnlyList<string> LinkageSignals,
    IReadOnlyList<string> Conflicts);
