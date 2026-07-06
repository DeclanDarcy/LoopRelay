namespace LoopRelay.Decisions.Models;

public sealed record DecisionProposalLineageEvent(
    DateTimeOffset OccurredAt,
    string Kind,
    string? ItemId,
    string Summary,
    string? FromState,
    string? ToState,
    IReadOnlyList<DecisionSourceReference> Sources);
