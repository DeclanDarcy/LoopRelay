using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record RecommendationEvidence(
    RecommendationEvidenceType Type,
    string OptionId,
    string Summary,
    IReadOnlyList<DecisionEvidence> Evidence);
