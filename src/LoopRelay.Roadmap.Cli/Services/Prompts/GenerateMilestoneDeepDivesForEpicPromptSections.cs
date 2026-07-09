namespace LoopRelay.Roadmap.Cli.Services.Prompts;

internal sealed record GenerateMilestoneDeepDivesForEpicPromptSectionSet(
    string GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance,
    string GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits,
    IReadOnlyDictionary<string, string> ActiveSectionSourceHashes);

internal static class GenerateMilestoneDeepDivesForEpicPromptSections
{
    public static GenerateMilestoneDeepDivesForEpicPromptSectionSet ForAuxiliaryArtifactPolicy(
        bool allowAuxiliaryNonImplementationFiles)
    {
        if (allowAuxiliaryNonImplementationFiles)
        {
            return new(
                string.Empty,
                string.Empty,
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        return new(
            Core.Prompts.NonImplementation.GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.Text,
            Core.Prompts.NonImplementation.GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.Text,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance"] =
                    Core.Prompts.NonImplementation.GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance.SourceHash,
                ["GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits"] =
                    Core.Prompts.NonImplementation.GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits.SourceHash,
            });
    }
}
