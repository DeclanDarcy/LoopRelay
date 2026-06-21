using CommandCenter.Continuity.Primitives;

namespace CommandCenter.Continuity.Models;

public sealed class OperationalContextItem
{
    public string Id { get; init; } = string.Empty;

    public OperationalContextItemKind Kind { get; init; }

    public string Text { get; init; } = string.Empty;

    public string? Rationale { get; init; }

    public string? SourceRelativePath { get; init; }
}
