using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Roadmap.Cli.Services.Workflows;

internal static class TraditionalRoadmapWorkflowDefinition
{
    public static WorkflowDefinition Create() =>
        CanonicalWorkflowDefinitionSketches.CreateTraditionalRoadmap();
}
