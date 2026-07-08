namespace LoopRelay.Roadmap.Cli.Primitives.State;

internal enum RoadmapUnblockAction
{
    ReportOnly,
    RecoverToCoreReady,
    RecoverExecutionDisposition,
    RecoverCompletionCertification,
    RecoverExecutionRuntimeFailure,
}
