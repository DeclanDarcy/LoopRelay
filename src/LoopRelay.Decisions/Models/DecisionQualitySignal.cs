using LoopRelay.Decisions.Primitives;

namespace LoopRelay.Decisions.Models;

public sealed record DecisionQualitySignal(
    string Id,
    Guid RepositoryId,
    string DecisionId,
    string Category,
    QualitySignalDirection Direction,
    QualitySignalSeverity Severity,
    string Summary,
    string Detail,
    IReadOnlyList<DecisionSourceReference> Sources);
