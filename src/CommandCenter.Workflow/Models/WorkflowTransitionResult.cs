using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowTransitionResult(
    WorkflowTransition Transition,
    bool IsValid,
    bool IsBlocked,
    WorkflowGateResolution? GateResolution,
    WorkflowBlockingCondition? BlockingCondition,
    string Reason);
