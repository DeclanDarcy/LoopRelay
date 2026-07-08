namespace LoopRelay.Roadmap.Cli.Models.Projections;

internal sealed record ProjectionProvenance(
    string ProjectionIdentity,
    string RuntimePromptName,
    string ProjectionPath,
    ProjectionPromptMetadata Prompt,
    IReadOnlyList<string> ProjectContextFiles,
    string ProjectContextHash,
    IReadOnlyList<ProjectionCausalInput> CausalInputs)
{
    public const string ProjectContextInputKind = "ProjectContext";
    public const string ProjectionPromptTemplateInputKind = "ProjectionPromptTemplate";

    public static ProjectionProvenance Create(
        ProjectionDefinition definition,
        ProjectionPromptMetadata prompt,
        ProjectContext projectContext) =>
        new(
            definition.RuntimePromptName,
            definition.RuntimePromptName,
            definition.ProjectionPath,
            prompt,
            projectContext.SourceFiles,
            projectContext.Hash,
            [
                new ProjectionCausalInput(ProjectContextInputKind, "ProjectContext", projectContext.Hash),
                new ProjectionCausalInput(ProjectionPromptTemplateInputKind, prompt.PromptName, prompt.SourceHash),
            ]);
}
