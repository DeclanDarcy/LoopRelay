namespace CommandCenter.Decisions.Models;

public sealed record DecisionOptionValidationResult(
    string OptionId,
    bool IsValid,
    IReadOnlyList<DecisionOptionValidationIssue> Issues);
