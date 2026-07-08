using System.Text;

namespace LoopRelay.Roadmap.Cli;

internal enum RoadmapExecutionOutcomeKind
{
    EpicComplete,
    ContinueRequired,
    ExecutionBlocked,
    RuntimeFailure,
    MalformedOutput,
}
