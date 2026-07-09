namespace LoopRelay.Roadmap.Cli.Services.Prompts;

internal sealed record CreateNewEpicPromptSectionSet(
    string EpicImplementationFirstGuidance,
    string EpicAuxiliaryArtifactLimits,
    IReadOnlyDictionary<string, string> ActiveSectionSourceHashes);

internal static class CreateNewEpicPromptSections
{
    public static CreateNewEpicPromptSectionSet ForAuxiliaryArtifactPolicy(
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
            Core.Prompts.NonImplementation.CreateNewEpicImplementationFirstGuidance.Text,
            Core.Prompts.NonImplementation.CreateNewEpicAuxiliaryArtifactLimits.Text,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CreateNewEpicImplementationFirstGuidance"] =
                    Core.Prompts.NonImplementation.CreateNewEpicImplementationFirstGuidance.SourceHash,
                ["CreateNewEpicAuxiliaryArtifactLimits"] =
                    Core.Prompts.NonImplementation.CreateNewEpicAuxiliaryArtifactLimits.SourceHash,
            });
    }
}
