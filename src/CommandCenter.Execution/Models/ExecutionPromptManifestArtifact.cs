namespace CommandCenter.Execution.Models;

public sealed class ExecutionPromptManifestArtifact
{
    public string Role { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public long? ByteCount { get; init; }

    public long? CharacterCount { get; init; }

    public bool Delivered { get; init; }
}
