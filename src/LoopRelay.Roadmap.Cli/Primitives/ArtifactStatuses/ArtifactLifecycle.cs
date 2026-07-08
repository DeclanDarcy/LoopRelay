namespace LoopRelay.Roadmap.Cli.Primitives.ArtifactStatuses;

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
