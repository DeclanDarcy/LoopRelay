namespace CommandCenter.Decisions.Models;

public sealed record DecisionRevisionFieldComparison(
    string Field,
    string ChangeType,
    string? PreviousValue,
    string? RevisedValue);
