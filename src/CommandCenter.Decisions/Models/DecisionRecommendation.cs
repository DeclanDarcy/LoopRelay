using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionRecommendation(
    string OptionId,
    string Rationale,
    IReadOnlyList<DecisionEvidence> Evidence)
{
    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> SupportingFactors { get; init; } = [];

    public IReadOnlyList<string> Concerns { get; init; } = [];

    public IReadOnlyList<string> Assumptions { get; init; } = [];

    public IReadOnlyList<string> AlternativeExplanations { get; init; } = [];

    public RecommendationMode Mode { get; init; } = RecommendationMode.PreferredOption;

    public IReadOnlyList<RecommendationEvidence> RecommendationEvidence { get; init; } = [];

    public IReadOnlyList<OptionEvaluation> OptionEvaluations { get; init; } = [];
}
