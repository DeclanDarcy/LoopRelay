using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationInsightSynthesis;

public sealed record NonImplementationSynthesisDecisionMetadata(
    NonImplementationSynthesisDecision Decision,
    string SynthesisPath,
    string DecisionArtifactPath,
    string DecisionSourceHash,
    DateTimeOffset DecidedAtUtc,
    string? Rationale = null,
    string? DecidedBy = null);
