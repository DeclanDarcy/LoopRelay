using LoopRelay.Roadmap.Cli.Services.Prompts;

namespace LoopRelay.Roadmap.Cli.Models.Projections;

internal sealed record ProjectionDefinition(
    string RuntimePromptName,
    string ProjectionPromptName,
    string ProjectionPath)
{
    public string RenderPrompt(string projectContext) =>
        RoadmapPromptCatalog.RenderProjection(ProjectionPromptName, projectContext);
}
