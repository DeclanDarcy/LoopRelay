namespace LoopRelay.Decisions.Models;

public sealed record DecisionTradeoffRevision(
    string OptionId,
    string ChangeType,
    string Reason,
    DecisionTradeoff? PreviousTradeoff = null,
    DecisionTradeoff? RevisedTradeoff = null);
