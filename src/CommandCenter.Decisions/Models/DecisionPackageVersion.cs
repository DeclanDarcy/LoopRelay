namespace CommandCenter.Decisions.Models;

public sealed record DecisionPackageVersion(
    string Id,
    Guid RepositoryId,
    string ProposalId,
    string CandidateId,
    DateTimeOffset CreatedAt,
    string PackageFingerprint,
    DecisionPackage Package);
