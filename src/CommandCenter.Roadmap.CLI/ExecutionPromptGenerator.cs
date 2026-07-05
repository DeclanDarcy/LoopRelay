namespace CommandCenter.Roadmap.Cli;

internal sealed class ExecutionPromptGenerator(RoadmapArtifacts artifacts, ArtifactLifecycleStore lifecycleStore)
{
    public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string operationalContext = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.OperationalContext);
        string activeEpic = await artifacts.ReadRequiredAsync(RoadmapArtifactPaths.ActiveEpic);
        IReadOnlyList<string> specs = (await artifacts.ListAsync(RoadmapArtifactPaths.SpecsDirectory, "*.md"))
            .Where(RoadmapArtifactPaths.IsMilestoneSpecPath)
            .ToArray();
        if (specs.Count == 0)
        {
            throw new RoadmapStepException("Cannot generate execution prompt without milestone specs.");
        }

        string firstSpecPath = specs.Order(StringComparer.Ordinal).First();
        string firstSpec = await artifacts.ReadRequiredAsync(firstSpecPath);
        string content =
            $"""
            # Roadmap Execution Prompt

            ## Execution Scope

            Execute the active epic through the ordered milestone specs. Start with `{firstSpecPath}`.

            ## Active Epic

            {activeEpic}

            ## First Executable Spec

            {firstSpec}

            ## Operational Context

            {operationalContext}
            """;

        if (content.Contains("<!-- BEGIN PROJECT-CONTEXT FILE:", StringComparison.Ordinal))
        {
            throw new RoadmapStepException("Execution prompt contains raw Project Context markers.");
        }

        await artifacts.WriteAsync(RoadmapArtifactPaths.ExecutionPrompt, content);
        await lifecycleStore.UpsertAsync(RoadmapArtifactPaths.ExecutionPrompt, ArtifactLifecycleState.Ready);
        return content;
    }
}
