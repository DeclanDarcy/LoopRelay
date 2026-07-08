using LoopRelay.Projections.Models;

namespace LoopRelay.Projections.Services;

public sealed class ProjectionProvenanceFactory
{
    public ProjectionProvenance Create(ProjectionDefinition definition, ProjectContext projectContext) =>
        ProjectionProvenance.Create(
            definition,
            ProjectionPromptCatalog.GetProjectionMetadata(definition.ProjectionPromptName),
            projectContext);
}
