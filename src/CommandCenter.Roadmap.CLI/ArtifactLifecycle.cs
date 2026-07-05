namespace CommandCenter.Roadmap.Cli;

internal enum ArtifactLifecycleState
{
    Missing,
    Draft,
    Ready,
    Executing,
    Completed,
    Archived,
    Superseded,
    Blocked,
}

internal sealed record ArtifactLifecycleEntry(
    string Path,
    ArtifactLifecycleState State,
    DateTimeOffset UpdatedAt,
    string Notes);
