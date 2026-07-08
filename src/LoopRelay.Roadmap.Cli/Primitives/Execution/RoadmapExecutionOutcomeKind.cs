namespace LoopRelay.Roadmap.Cli.Primitives.Execution;

internal enum RoadmapExecutionOutcomeKind
{
    EpicComplete,
    ContinueRequired,
    ExecutionBlocked,
    RuntimeFailure,
    MalformedOutput,
}
