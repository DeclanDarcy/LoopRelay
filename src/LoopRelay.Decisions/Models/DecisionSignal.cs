using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionSignal(
    string Kind,
    string Summary,
    DecisionClassification Classification,
    DecisionCandidatePriority Priority,
    IReadOnlyList<DecisionEvidence> Evidence);
