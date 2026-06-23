namespace CommandCenter.Decisions.Models;

public sealed record DecisionPackageValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
