namespace LoopRelay.Projections;

public sealed record ProjectionDefinition(
    string RuntimePromptName,
    string ProjectionPromptName,
    string ProjectionPath,
    string RequiredTitle,
    string IntendedConsumer)
{
    public string RenderPrompt(string projectContext) =>
        ProjectionPromptCatalog.RenderProjection(ProjectionPromptName, projectContext);
}
