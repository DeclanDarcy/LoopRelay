using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionResolution(
    DecisionOutcome Outcome,
    string SelectedOptionId,
    string Rationale,
    string ResolvedBy,
    bool RecommendationDiverged,
    DateTimeOffset ResolvedAt,
    IReadOnlyList<DecisionSourceReference> Sources,
    DecisionResolvedProposalSnapshot? SourceProposalSnapshot = null);
