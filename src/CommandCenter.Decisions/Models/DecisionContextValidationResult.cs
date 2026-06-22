namespace CommandCenter.Decisions.Models;

public sealed record DecisionContextValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
