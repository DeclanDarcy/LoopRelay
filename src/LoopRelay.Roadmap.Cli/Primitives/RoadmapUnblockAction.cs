using LoopRelay.Completion;

namespace LoopRelay.Roadmap.Cli;

internal enum RoadmapUnblockAction
{
    ReportOnly,
    RecoverToCoreReady,
    RecoverExecutionDisposition,
    RecoverCompletionCertification,
    RecoverExecutionRuntimeFailure,
}
