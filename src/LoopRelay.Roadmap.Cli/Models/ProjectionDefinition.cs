namespace LoopRelay.Roadmap.Cli;

internal sealed record ProjectionDefinition(
    string RuntimePromptName,
    string ProjectionPromptName,
    string ProjectionPath)
{
    public string RenderPrompt(string projectContext) =>
        RoadmapPromptCatalog.RenderProjection(ProjectionPromptName, projectContext);
}
