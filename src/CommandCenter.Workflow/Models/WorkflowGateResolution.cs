using CommandCenter.Workflow.Primitives;

namespace CommandCenter.Workflow.Models;

public sealed record WorkflowGateResolution(
    WorkflowGateType GateType,
    WorkflowBlockingCondition BlockingCondition,
    string RequiredHumanAction,
    bool IsSatisfied);
