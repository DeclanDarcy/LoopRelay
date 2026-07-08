using LoopRelay.Orchestration.Primitives.NonImplementationReview;

namespace LoopRelay.Orchestration.Models.NonImplementationSemanticConfirmation;

public sealed record NonImplementationSemanticConfirmation(
    string LedgerEntryId,
    string CandidatePath,
    string? ReviewedContentSha256,
    bool ReviewedFileDeleted,
    string? DeletedReviewedIdentity,
    NonImplementationSemanticDisposition Disposition,
    string Rationale,
    IReadOnlyList<string> EvidenceExcerptsOrPathFacts,
    string? UncertaintyNote = null);
