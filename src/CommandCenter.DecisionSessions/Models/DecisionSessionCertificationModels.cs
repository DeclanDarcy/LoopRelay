using CommandCenter.DecisionSessions.Primitives;

namespace CommandCenter.DecisionSessions.Models;

public sealed record DecisionSessionCertificationFinding(
    string Id,
    string Category,
    bool Passed,
    string Summary,
    string Detail,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionSessionGovernanceReport(
    Guid RepositoryId,
    DecisionSessionProjection? ActiveSession,
    int SessionCount,
    int ActiveSessionCount,
    DecisionSessionLifecycleDecision? PolicyDecision,
    DecisionSessionTransferEligibilityStatus? TransferEligibilityStatus,
    IReadOnlyList<string> Diagnostics,
    DateTimeOffset GeneratedAt);

public sealed record DecisionSessionHealthReport(
    Guid RepositoryId,
    DecisionSessionHealthStatus OverallStatus,
    IReadOnlyList<DecisionSessionHealthDimension> Dimensions,
    IReadOnlyList<string> Diagnostics,
    DateTimeOffset GeneratedAt);

public sealed record DecisionSessionCertificationResult(
    string Id,
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    string InputFingerprint,
    bool Certified,
    int PassedFindingCount,
    int FailedFindingCount,
    IReadOnlyList<DecisionSessionCertificationFinding> Findings,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Diagnostics);

public sealed record DecisionSessionCertificationReport(
    string ReportId,
    Guid RepositoryId,
    DateTimeOffset GeneratedAt,
    DecisionSessionCertificationResult Result,
    DecisionSessionGovernanceReport Governance,
    DecisionSessionHealthReport Health);

public sealed record DecisionSessionLifecycleEndToEndFixture(
    Guid RepositoryId,
    DecisionSessionId? InitialSessionId,
    DecisionSessionId? ReplacementSessionId,
    string? ContinuityArtifactId,
    string? TransferId,
    string? RecoveryId,
    bool CertificationPassed,
    DateTimeOffset GeneratedAt);
