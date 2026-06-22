namespace CommandCenter.Execution.Models;

public sealed class ExecutionContextArtifact
{
    public string Role { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public long ByteCount { get; init; }

    public long CharacterCount { get; init; }
}
