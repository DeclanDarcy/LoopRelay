using LoopRelay.Projections.Models.Context;
using LoopRelay.Projections.Models.Definitions;
using LoopRelay.Projections.Models.Provenance;
using LoopRelay.Projections.Services.Prompts;

namespace LoopRelay.Projections.Services.Provenance;

public sealed class ProjectionProvenanceFactory
{
    public ProjectionProvenance Create(ProjectionDefinition definition, ProjectContext projectContext) =>
        ProjectionProvenance.Create(
            definition,
            ProjectionPromptCatalog.GetProjectionMetadata(definition.ProjectionPromptName),
            projectContext);
}
