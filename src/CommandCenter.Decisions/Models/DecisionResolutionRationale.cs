namespace CommandCenter.Decisions.Models;

public sealed record DecisionResolutionRationale(
    string Text,
    string SelectedOptionId,
    bool RecommendationDiverged);
