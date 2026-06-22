using CommandCenter.Continuity.Primitives;

namespace CommandCenter.Continuity.Models;

public sealed class OperationalContextSemanticChange
{
    public OperationalContextSemanticChangeType Type { get; init; }

    public string Section { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? ItemId { get; init; }
}
