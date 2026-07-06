using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionSourceReference(
    string SourceKind,
    string? RelativePath = null,
    string? Section = null,
    string? ItemId = null,
    DecisionId? DecisionId = null,
    string? ProposalId = null,
    string? CandidateId = null,
    string? Excerpt = null);
