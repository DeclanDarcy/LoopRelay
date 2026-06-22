using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record ExecutionConstraint(
    string Id,
    string DecisionId,
    string Title,
    string Statement,
    DecisionClassification Classification,
    IReadOnlyList<DecisionSourceReference> Sources);
