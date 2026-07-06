namespace LoopRelay.Continuity.Models;

public sealed class OperationalContextCompressionOutcome
{
    public string Outcome { get; init; } = string.Empty;

    public string ItemKind { get; init; } = string.Empty;

    public string ItemText { get; init; } = string.Empty;

    public string Rule { get; init; } = string.Empty;

    public string Threshold { get; init; } = string.Empty;

    public string Rationale { get; init; } = string.Empty;

    public IReadOnlyList<string> Evidence { get; init; } = [];
}
