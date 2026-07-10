using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Cli.Services.Workflows;

internal static class EvalRoadmapWorkflowDefinition
{
    public static WorkflowDefinition Create() =>
        CanonicalWorkflowDefinitionSketches.CreateEvalRoadmap();
}
