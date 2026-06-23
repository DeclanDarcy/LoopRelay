using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record ExecutionArchitectureRule(
    string Id,
    string DecisionId,
    string Title,
    string Statement,
    DecisionClassification Classification,
    ExecutionProjectionKind ProjectionKind,
    IReadOnlyList<DecisionSourceReference> Sources);
