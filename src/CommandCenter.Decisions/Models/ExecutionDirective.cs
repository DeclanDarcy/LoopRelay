using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record ExecutionDirective(
    string Id,
    string DecisionId,
    string Title,
    string Statement,
    DecisionClassification Classification,
    IReadOnlyList<DecisionSourceReference> Sources);
