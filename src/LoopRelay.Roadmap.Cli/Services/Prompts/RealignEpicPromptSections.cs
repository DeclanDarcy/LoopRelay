namespace LoopRelay.Roadmap.Cli.Services.Prompts;

internal sealed record RealignEpicPromptSectionSet(
    string RealignEpicImplementationFirstGuidance,
    string RealignEpicAuxiliaryArtifactLimits,
    IReadOnlyDictionary<string, string> ActiveSectionSourceHashes);

internal static class RealignEpicPromptSections
{
    public static RealignEpicPromptSectionSet ForAuxiliaryArtifactPolicy(
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
            Core.Prompts.NonImplementation.RealignEpicImplementationFirstGuidance.Text,
            Core.Prompts.NonImplementation.RealignEpicAuxiliaryArtifactLimits.Text,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["RealignEpicImplementationFirstGuidance"] =
                    Core.Prompts.NonImplementation.RealignEpicImplementationFirstGuidance.SourceHash,
                ["RealignEpicAuxiliaryArtifactLimits"] =
                    Core.Prompts.NonImplementation.RealignEpicAuxiliaryArtifactLimits.SourceHash,
            });
    }
}
