using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionGovernanceReport(
    string Id,
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    string InputFingerprint,
    DecisionHealthAssessment Health,
    DecisionGovernanceSummary Summary,
    IReadOnlyList<DecisionGovernanceFinding> Findings,
    IReadOnlyList<string> Diagnostics);
