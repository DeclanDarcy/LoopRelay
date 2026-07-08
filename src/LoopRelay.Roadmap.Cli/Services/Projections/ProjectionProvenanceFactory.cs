using LoopRelay.Roadmap.Cli.Models.Projections;
using LoopRelay.Roadmap.Cli.Services.Prompts;

namespace LoopRelay.Roadmap.Cli.Services.Projections;

internal sealed class ProjectionProvenanceFactory(ProjectionRegistry registry)
{
    private readonly ProjectionRegistry _registry = registry;
    public ProjectionProvenance Create(string runtimePromptName, ProjectContext projectContext) =>
        Create(_registry.Get(runtimePromptName), projectContext);

    public ProjectionProvenance Create(ProjectionDefinition definition, ProjectContext projectContext) =>
        ProjectionProvenance.Create(
            definition,
            RoadmapPromptCatalog.GetProjectionMetadata(definition.ProjectionPromptName),
            projectContext);
}
