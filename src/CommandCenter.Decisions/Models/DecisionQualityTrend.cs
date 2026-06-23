using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionQualityTrend(
    string Id,
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    int AssessmentCount,
    DecisionQualityRating CurrentRating,
    DecisionQualityRating PreviousRating,
    double CurrentAverageScore,
    double PreviousAverageScore,
    QualitySignalDirection Direction,
    IReadOnlyList<string> Diagnostics);
