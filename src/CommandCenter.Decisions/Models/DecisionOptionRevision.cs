namespace CommandCenter.Decisions.Models;

public sealed record DecisionOptionRevision(
    string OptionId,
    string ChangeType,
    string Reason,
    DecisionOption? PreviousOption = null,
    DecisionOption? RevisedOption = null);
