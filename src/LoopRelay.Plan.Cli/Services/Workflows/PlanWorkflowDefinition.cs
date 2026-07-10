using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Plan.Cli.Services.Workflows;

internal static class PlanWorkflowDefinition
{
    public static WorkflowDefinition Create() =>
        CanonicalWorkflowDefinitionSketches.CreatePlan();
}
