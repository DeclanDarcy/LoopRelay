using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionCandidate(
    string Id,
    Guid RepositoryId,
    DecisionCandidateState State,
    DecisionCandidatePriority Priority,
    string Title,
    string Summary,
    string SourceFingerprint,
    IReadOnlyList<DecisionSourceReference> Sources,
    IReadOnlyList<DecisionHistoryEntry> History);
