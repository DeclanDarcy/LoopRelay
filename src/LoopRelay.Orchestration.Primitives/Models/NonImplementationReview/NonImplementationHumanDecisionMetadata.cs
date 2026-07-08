namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationHumanDecisionMetadata(
    NonImplementationResolutionState ResolutionState,
    string DecisionArtifactPath,
    string DecisionSourceHash,
    DateTimeOffset DecidedAtUtc,
    string? Rationale = null,
    string? DecidedBy = null,
    string? ReviewedContentSha256 = null,
    string? ReviewedDeletedIdentity = null);
