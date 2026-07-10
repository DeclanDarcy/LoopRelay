using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Cli.Services.Workflows;

internal static class ExecuteWorkflowDefinition
{
    public static WorkflowDefinition Create() =>
        CanonicalWorkflowDefinitionSketches.CreateExecute();
}
