namespace LoopRelay.Continuity.Models;

public sealed class OperationalContextSection
{
    public string Heading { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public IReadOnlyList<OperationalContextItem> Items { get; init; } = [];
}
