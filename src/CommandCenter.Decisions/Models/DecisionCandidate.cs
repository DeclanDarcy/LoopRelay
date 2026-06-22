using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionCandidate(
    string Id,
    Guid RepositoryId,
    DecisionCandidateState State,
    DecisionCandidatePriority Priority,
    DecisionClassification Classification,
    string Title,
    string Summary,
    string SourceFingerprint,
    IReadOnlyList<DecisionSignal> Signals,
    IReadOnlyList<DecisionEvidence> Evidence,
    IReadOnlyList<DecisionSourceReference> Sources,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<DecisionHistoryEntry> History);
