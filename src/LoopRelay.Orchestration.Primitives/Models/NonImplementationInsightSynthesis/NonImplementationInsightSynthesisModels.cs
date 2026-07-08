using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationInsightSynthesis;

public sealed record NonImplementationInsightSynthesisSource(
    string EntryId,
    string Path,
    string? ReviewedContentSha256,
    bool ReviewedFileDeleted,
    string? DeletedReviewedIdentity,
    NonImplementationSemanticDisposition SemanticDisposition);
