using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionSignal(
    string Kind,
    string Summary,
    DecisionClassification Classification,
    DecisionCandidatePriority Priority,
    IReadOnlyList<DecisionEvidence> Evidence);
