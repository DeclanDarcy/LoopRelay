using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ExecutionDispositionRoute(
    ExecutionDispositionStatus Status,
    ExecutionDispositionCommand Command,
    RoadmapExecutionOutcomeKind OutcomeKind,
    RoadmapState TargetState,
    string WorkflowTransition);
