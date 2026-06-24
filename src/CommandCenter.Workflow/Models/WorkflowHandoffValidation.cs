namespace CommandCenter.Workflow.Models;

public sealed record WorkflowHandoffValidation(
    bool IsValid,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Failures);
