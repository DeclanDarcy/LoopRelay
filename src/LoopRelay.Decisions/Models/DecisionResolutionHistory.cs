namespace LoopRelay.Decisions.Models;

public sealed record DecisionResolutionHistory(
    string ProposalId,
    string DecisionId,
    DateTimeOffset ResolvedAt,
    string ResolvedBy,
    DecisionResolutionRationale Rationale);
