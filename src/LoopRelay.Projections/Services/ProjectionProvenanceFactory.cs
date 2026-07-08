namespace LoopRelay.Projections;

public sealed class ProjectionProvenanceFactory
{
    public ProjectionProvenance Create(ProjectionDefinition definition, ProjectContext projectContext) =>
        ProjectionProvenance.Create(
            definition,
            ProjectionPromptCatalog.GetProjectionMetadata(definition.ProjectionPromptName),
            projectContext);
}
