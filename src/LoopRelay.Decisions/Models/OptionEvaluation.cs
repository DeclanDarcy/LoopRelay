namespace LoopRelay.Decisions.Models;

public sealed record OptionEvaluation(
    string OptionId,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Constraints,
    string Summary,
    int Score,
    int Rank,
    string ScoreExplanation,
    IReadOnlyList<RecommendationEvidence> Evidence);
