using LoopRelay.Roadmap.Cli.Primitives.Execution;

namespace LoopRelay.Roadmap.Cli.Models.Execution;

internal sealed record ExecutionDispositionRoute(
    ExecutionDispositionStatus Status,
    ExecutionDispositionCommand Command,
    RoadmapExecutionOutcomeKind OutcomeKind,
    Primitives.State.RoadmapState TargetState,
    string WorkflowTransition);
