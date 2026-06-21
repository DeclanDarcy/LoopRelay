namespace CommandCenter.Core.Continuity;

public sealed class DecisionArtifactInput
{
    public string RelativePath { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public bool IsCurrent { get; init; }
}
