using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionQualityAssessment(
    string Id,
    Guid RepositoryId,
    string DecisionId,
    DateTimeOffset AssessedAt,
    DecisionQualityRating Rating,
    int Score,
    IReadOnlyList<DecisionQualitySignal> Signals,
    IReadOnlyList<HumanAuthoringBurdenSignal> HumanAuthoringBurdenSignals,
    IReadOnlyList<string> Diagnostics,
    DecisionQualityExplanation? QualityExplanation = null,
    HumanAuthoringBurdenExplanation? HumanAuthoringBurdenExplanation = null);
