namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationHitlRequestEntry(
    string DeliverablePathOrPattern,
    string SourceArtifactPath,
    string SourceHash,
    NonImplementationHitlProvenanceKind HitlProvenanceKind,
    string Rationale,
    DateTimeOffset FirstCapturedAtUtc,
    string? EvidenceExcerpt = null);
