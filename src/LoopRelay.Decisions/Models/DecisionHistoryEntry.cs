namespace LoopRelay.Decisions.Models;

public sealed record DecisionHistoryEntry(
    DateTimeOffset Timestamp,
    string Event,
    string? FromState,
    string ToState,
    string? Reason,
    IReadOnlyList<DecisionSourceReference> Sources);
