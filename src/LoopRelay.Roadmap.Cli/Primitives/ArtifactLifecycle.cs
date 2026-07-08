namespace LoopRelay.Roadmap.Cli.Primitives;

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
