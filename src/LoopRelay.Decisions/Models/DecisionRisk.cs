using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionRisk(
    string Statement,
    TradeoffSeverity Severity,
    bool IsUnknown,
    IReadOnlyList<DecisionEvidence> Evidence);
