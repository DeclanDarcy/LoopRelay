using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration;
using LoopRelay.Projections;

namespace LoopRelay.Completion;

public enum CompletionCertificationServiceOutcome
{
    Completed,
    Blocked,
    Failed,
}
