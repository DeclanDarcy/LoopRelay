namespace LoopRelay.Decisions.Models;

public sealed record DecisionReviewAuthority(
    string ProposalFingerprint,
    string? PackageId,
    string? PackageFingerprint,
    DateTimeOffset? PackageVersionCreatedAt,
    string? PackageSourceProposalFingerprint,
    bool IsPackageCurrentForProposalContent);
