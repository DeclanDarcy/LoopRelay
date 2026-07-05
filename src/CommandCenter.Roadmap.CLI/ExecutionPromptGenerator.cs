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
        string validProtocolPairs = string.Join(
            Environment.NewLine,
            ExecutionDispositionProtocol.ValidPairDescriptions.Select(pair => $"- `{pair}`"));
        string epicComplete = ExecutionDispositionProtocol.StatusText(ExecutionDispositionStatus.EpicComplete);
        string continueRequired = ExecutionDispositionProtocol.StatusText(ExecutionDispositionStatus.ContinueRequired);
        string executionBlocked = ExecutionDispositionProtocol.StatusText(ExecutionDispositionStatus.ExecutionBlocked);
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

            ## Required Execution Disposition

            End your response with this exact section. The roadmap runtime will parse it to decide the next workflow transition. Do not omit it.

            ```markdown
            ## Execution Disposition

            | Field | Value |
            |---|---|
            | Status | {ExecutionDispositionProtocol.StatusOptionsText} |
            | Confidence | High OR Medium OR Low OR Unclear |
            | Evidence Summary | Concise implementation evidence, continuation reason, or blocker summary. |
            | Next Step | {ExecutionDispositionProtocol.CommandOptionsText} |
            ```

            The execution disposition is a protocol message. Use only these valid state/command pairs:

            {validProtocolPairs}

            Use `{epicComplete}` only when you explicitly claim the active epic implementation is complete and ready for independent completion certification.
            Use `{continueRequired}` when the execution turn finished successfully but more implementation work remains.
            Use `{executionBlocked}` when execution cannot proceed without resolving a domain blocker or required human intervention.
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
