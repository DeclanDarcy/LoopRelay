namespace CommandCenter.Workflow.Models;

public sealed record WorkflowGateHistoryProjection(
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<WorkflowGate> Gates,
    string Markdown);
