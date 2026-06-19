namespace CommandCenter.Backend.Artifacts;

public enum ArtifactType
{
    Plan,
    OperationalContext,
    Milestone,
    Handoff,
    Decision
}

public enum ArtifactFamily
{
    Plan,
    OperationalContext,
    Milestone,
    Handoff,
    Decision
}

public enum ArtifactVersionKind
{
    Current,
    Historical
}

public sealed class Artifact
{
    public string RelativePath { get; init; } = "";

    public string Name { get; init; } = "";

    public ArtifactType Type { get; init; }

    public ArtifactFamily Family { get; init; }

    public ArtifactVersionKind VersionKind { get; init; }
}
