namespace LoopRelay.Roadmap.Cli.Services.Prompts;

internal sealed record SplitEpicPromptSectionSet(
    string SplitEpicImplementationFirstGuidance,
    string SplitEpicAuxiliaryArtifactLimits,
    IReadOnlyDictionary<string, string> ActiveSectionSourceHashes);

internal static class SplitEpicPromptSections
{
    public static SplitEpicPromptSectionSet ForAuxiliaryArtifactPolicy(
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
            Core.Prompts.NonImplementation.SplitEpicImplementationFirstGuidance.Text,
            Core.Prompts.NonImplementation.SplitEpicAuxiliaryArtifactLimits.Text,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["SplitEpicImplementationFirstGuidance"] =
                    Core.Prompts.NonImplementation.SplitEpicImplementationFirstGuidance.SourceHash,
                ["SplitEpicAuxiliaryArtifactLimits"] =
                    Core.Prompts.NonImplementation.SplitEpicAuxiliaryArtifactLimits.SourceHash,
            });
    }
}
