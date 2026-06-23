using CommandCenter.Decisions.Primitives;

namespace CommandCenter.Decisions.Models;

public sealed record DecisionRisk(
    string Statement,
    TradeoffSeverity Severity,
    bool IsUnknown,
    IReadOnlyList<DecisionEvidence> Evidence);
