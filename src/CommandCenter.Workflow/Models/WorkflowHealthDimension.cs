namespace CommandCenter.Workflow.Models;

public sealed record WorkflowHealthDimension(
    string Name,
    string Status,
    string Reason,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Diagnostics);
