using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationReview;

public sealed record NonImplementationInsightSynthesisSource(
    string EntryId,
    string Path,
    string? ReviewedContentSha256,
    bool ReviewedFileDeleted,
    string? DeletedReviewedIdentity,
    NonImplementationSemanticDisposition SemanticDisposition);
