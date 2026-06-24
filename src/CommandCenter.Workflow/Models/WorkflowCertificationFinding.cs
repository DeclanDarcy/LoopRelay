namespace CommandCenter.Workflow.Models;

public sealed record WorkflowCertificationFinding(
    string Id,
    string Category,
    bool Passed,
    string Summary,
    string Detail,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Diagnostics);
