using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionResolution(
    DecisionOutcome Outcome,
    string Rationale,
    DateTimeOffset ResolvedAt,
    IReadOnlyList<DecisionSourceReference> Sources);
