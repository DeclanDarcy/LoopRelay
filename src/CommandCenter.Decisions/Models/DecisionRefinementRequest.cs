namespace CommandCenter.Decisions.Models;

public sealed record DecisionRefinementRequest(
    string Reason,
    string? Context = null,
    IReadOnlyList<DecisionOption>? Options = null,
    IReadOnlyList<DecisionTradeoff>? Tradeoffs = null,
    DecisionRecommendation? Recommendation = null,
    IReadOnlyList<DecisionAssumption>? Assumptions = null,
    string? RequestedBy = null,
    string? BaseProposalFingerprint = null,
    IReadOnlyList<DecisionConstraint>? Constraints = null,
    IReadOnlyList<DecisionAssumptionRevision>? AssumptionRevisions = null,
    IReadOnlyList<DecisionOptionRevision>? OptionRevisions = null,
    IReadOnlyList<DecisionTradeoffRevision>? TradeoffRevisions = null,
    IReadOnlyList<string>? RejectedChanges = null);
