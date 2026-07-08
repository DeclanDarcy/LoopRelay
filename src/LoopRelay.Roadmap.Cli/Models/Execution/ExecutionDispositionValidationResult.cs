namespace LoopRelay.Roadmap.Cli.Models.Execution;

internal sealed record ExecutionDispositionValidationResult(
    bool IsValid,
    ExecutionDisposition Disposition,
    ExecutionDispositionRoute? Route,
    string? ViolationReason,
    string RequiredRecoveryPath)
{
    public static ExecutionDispositionValidationResult Valid(
        ExecutionDisposition disposition,
        ExecutionDispositionRoute route) =>
        new(true, disposition, route, null, route.WorkflowTransition);

    public static ExecutionDispositionValidationResult Invalid(
        ExecutionDisposition disposition,
        string violationReason) =>
        new(
            false,
            disposition,
            null,
            violationReason,
            "Review the raw execution output, correct the Execution Disposition to a valid protocol pair, and rerun the roadmap CLI.");
}
