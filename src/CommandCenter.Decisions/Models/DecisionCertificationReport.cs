using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionCertificationReport(
    string Id,
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    string InputFingerprint,
    DecisionLifecycleCertificationResult Result,
    DecisionHealthAssessment Health,
    IReadOnlyList<DecisionCertificationEvidence> Evidence,
    IReadOnlyList<DecisionGovernanceFinding> Findings,
    IReadOnlyList<string> Diagnostics);
