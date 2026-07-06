using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record ExecutionDirective(
    string Id,
    string DecisionId,
    string Title,
    string Statement,
    DecisionClassification Classification,
    ExecutionProjectionKind ProjectionKind,
    IReadOnlyList<DecisionSourceReference> Sources);
