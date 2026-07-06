namespace LoopRelay.Continuity.Models;

public sealed class OperationalEvolutionTimelineEntry
{
    public string Outcome { get; init; } = string.Empty;

    public string SemanticEventType { get; init; } = string.Empty;

    public string Section { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? ItemId { get; init; }

    public string? PreviousState { get; init; }

    public string? CurrentState { get; init; }

    public string? Reason { get; init; }

    public string? IdentityBasis { get; init; }

    public int? PreviousRevisionNumber { get; init; }

    public int? CurrentRevisionNumber { get; init; }

    public IReadOnlyList<string> SupportingEvidence { get; init; } = [];
}
