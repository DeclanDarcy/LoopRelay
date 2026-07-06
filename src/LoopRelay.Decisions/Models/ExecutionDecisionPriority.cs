using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record ExecutionDecisionPriority(
    string Id,
    string DecisionId,
    string Title,
    string Statement,
    DecisionClassification Classification,
    ExecutionProjectionKind ProjectionKind,
    int Rank,
    IReadOnlyList<DecisionSourceReference> Sources);
