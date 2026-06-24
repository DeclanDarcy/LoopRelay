namespace CommandCenter.Workflow.Models;

public sealed record WorkflowExecutionDiagnostics(
    Guid RepositoryId,
    IReadOnlyList<string> IncludedEvidence,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> Reasoning);
