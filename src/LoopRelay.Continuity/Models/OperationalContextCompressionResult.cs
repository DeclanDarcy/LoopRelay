namespace LoopRelay.Continuity.Models;

public sealed class OperationalContextCompressionResult
{
    public OperationalContextDocument Document { get; init; } = new();

    public OperationalContextCompressionSummary Summary { get; init; } = new();
}
