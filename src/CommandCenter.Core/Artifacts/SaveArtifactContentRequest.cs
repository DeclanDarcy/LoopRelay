namespace CommandCenter.Core.Artifacts;

public sealed class SaveArtifactContentRequest
{
    public string RelativePath { get; init; } = "";

    public string Content { get; init; } = "";
}
