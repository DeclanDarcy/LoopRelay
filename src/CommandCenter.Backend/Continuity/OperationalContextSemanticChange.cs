namespace CommandCenter.Backend.Continuity;

public sealed class OperationalContextSemanticChange
{
    public OperationalContextSemanticChangeType Type { get; init; }

    public string Section { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string? ItemId { get; init; }
}
