namespace LoopRelay.Roadmap.Cli.Services.Prompts;

internal sealed record ReimagineEpicPromptSectionSet(
    string ReimagineEpicImplementationFirstGuidance,
    string ReimagineEpicAuxiliaryArtifactLimits,
    IReadOnlyDictionary<string, string> ActiveSectionSourceHashes);

internal static class ReimagineEpicPromptSections
{
    public static ReimagineEpicPromptSectionSet ForAuxiliaryArtifactPolicy(
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
            Core.Prompts.NonImplementation.ReimagineEpicImplementationFirstGuidance.Text,
            Core.Prompts.NonImplementation.ReimagineEpicAuxiliaryArtifactLimits.Text,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ReimagineEpicImplementationFirstGuidance"] =
                    Core.Prompts.NonImplementation.ReimagineEpicImplementationFirstGuidance.SourceHash,
                ["ReimagineEpicAuxiliaryArtifactLimits"] =
                    Core.Prompts.NonImplementation.ReimagineEpicAuxiliaryArtifactLimits.SourceHash,
            });
    }
}
