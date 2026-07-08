namespace LoopRelay.Roadmap.Cli;

internal sealed class ProjectionProvenanceFactory(ProjectionRegistry registry)
{
    public ProjectionProvenance Create(string runtimePromptName, ProjectContext projectContext) =>
        Create(registry.Get(runtimePromptName), projectContext);

    public ProjectionProvenance Create(ProjectionDefinition definition, ProjectContext projectContext) =>
        ProjectionProvenance.Create(
            definition,
            RoadmapPromptCatalog.GetProjectionMetadata(definition.ProjectionPromptName),
            projectContext);
}
