namespace LoopRelay.Roadmap.Cli.Primitives;

internal enum RoadmapExecutionOutcomeKind
{
    EpicComplete,
    ContinueRequired,
    ExecutionBlocked,
    RuntimeFailure,
    MalformedOutput,
}
