using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record RecommendationEvidence(
    RecommendationEvidenceType Type,
    string OptionId,
    string Summary,
    IReadOnlyList<DecisionEvidence> Evidence);
