namespace CommandCenter.Decisions.Models;

public sealed record DecisionGenerationContextEntry(
    string Id,
    string Statement,
    IReadOnlyList<DecisionEvidence> Evidence);
