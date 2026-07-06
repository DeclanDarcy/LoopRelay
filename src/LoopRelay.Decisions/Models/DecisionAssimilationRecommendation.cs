namespace LoopRelay.Decisions.Models;

public sealed record DecisionAssimilationRecommendation(
    string DecisionId,
    Guid RepositoryId,
    DateTimeOffset CreatedAt,
    string DecisionFingerprint,
    string ContextSnapshotId,
    string ContextFingerprint,
    Decision SourceDecision,
    DecisionContextSnapshot ContextSnapshot,
    string ProjectedStableDecision,
    string Rationale,
    string? RequestedBy,
    string? Notes,
    IReadOnlyList<DecisionEvidence> Evidence,
    IReadOnlyList<DecisionSourceReference> Sources,
    IReadOnlyList<string> Diagnostics);
