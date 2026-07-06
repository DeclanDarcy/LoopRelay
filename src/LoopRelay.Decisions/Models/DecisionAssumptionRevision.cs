namespace LoopRelay.Decisions.Models;

public sealed record DecisionAssumptionRevision(
    string AssumptionId,
    string ChangeType,
    string Reason,
    string? PreviousStatement = null,
    string? RevisedStatement = null,
    IReadOnlyList<DecisionEvidence>? Evidence = null);
