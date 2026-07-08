namespace LoopRelay.Roadmap.Cli;

internal sealed record ExecutionDispositionRoute(
    ExecutionDispositionStatus Status,
    ExecutionDispositionCommand Command,
    RoadmapExecutionOutcomeKind OutcomeKind,
    RoadmapState TargetState,
    string WorkflowTransition);
