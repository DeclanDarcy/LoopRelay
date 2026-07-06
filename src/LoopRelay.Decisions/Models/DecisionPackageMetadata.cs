namespace LoopRelay.Decisions.Models;

public sealed record DecisionPackageMetadata(
    string ContextFingerprint,
    string GeneratorVersion,
    string CandidateId,
    string RepositoryStateFingerprint,
    string MilestoneId,
    string MilestonePath,
    string SourceProposalId,
    string SourceProposalFingerprint);
