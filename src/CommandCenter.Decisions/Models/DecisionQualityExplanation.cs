using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionQualityExplanation(
    int BaseScore,
    int RawScore,
    int ClampedScore,
    DecisionQualityThresholdExplanation Threshold,
    string? OverrideReason,
    IReadOnlyList<DecisionQualitySignalContribution> SignalContributions,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionQualitySignalContribution(
    string SignalId,
    string Category,
    QualitySignalDirection Direction,
    QualitySignalSeverity Severity,
    int ScoreContribution,
    string Summary);

public sealed record DecisionQualityThresholdExplanation(
    DecisionQualityRating Rating,
    int? MinimumScore,
    int? MaximumScore,
    string Reason);
