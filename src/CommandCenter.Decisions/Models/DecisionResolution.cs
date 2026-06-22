using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionResolution(
    DecisionOutcome Outcome,
    string SelectedOptionId,
    string Rationale,
    string ResolvedBy,
    bool RecommendationDiverged,
    DateTimeOffset ResolvedAt,
    IReadOnlyList<DecisionSourceReference> Sources,
    DecisionResolvedProposalSnapshot? SourceProposalSnapshot = null);
