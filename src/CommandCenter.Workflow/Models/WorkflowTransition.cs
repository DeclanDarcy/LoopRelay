using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowTransition(
    WorkflowStage FromStage,
    WorkflowStage ToStage,
    WorkflowGateType RequiredGate,
    WorkflowBlockingCondition? BlockingCondition,
    string Description);
