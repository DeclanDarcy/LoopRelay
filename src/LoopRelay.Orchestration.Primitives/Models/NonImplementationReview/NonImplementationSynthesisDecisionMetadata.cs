namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationSynthesisDecisionMetadata(
    NonImplementationSynthesisDecision Decision,
    string SynthesisPath,
    string DecisionArtifactPath,
    string DecisionSourceHash,
    DateTimeOffset DecidedAtUtc,
    string? Rationale = null,
    string? DecidedBy = null);
