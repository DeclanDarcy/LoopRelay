using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Services.State;

namespace LoopRelay.Roadmap.Cli.Services.Prompts;

internal sealed record RoadmapRuntimePromptPolicy(
    bool AllowAuxiliaryNonImplementationFiles,
    string LegacyImplementationFirstPromptPolicy)
{
    public static RoadmapRuntimePromptPolicy Default { get; } =
        FromArtifactPolicy(NonImplementationArtifactPolicyOptions.Default);

    public static RoadmapRuntimePromptPolicy FromArtifactPolicy(
        NonImplementationArtifactPolicyOptions artifactPolicy) =>
        new(
            artifactPolicy.AllowAuxiliaryNonImplementationFiles,
            ImplementationFirstPromptPolicyComposer.Compose(artifactPolicy));

    public TransitionPromptPolicyIdentity CreateIdentity(string runtimePromptName)
    {
        if (RoadmapPromptCatalog.UsesPromptOwnedNonImplementationPolicy(runtimePromptName))
        {
            CreateNewEpicPromptSectionSet sections =
                CreateNewEpicPromptSections.ForAuxiliaryArtifactPolicy(AllowAuxiliaryNonImplementationFiles);
            var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["allowAuxiliaryNonImplementationFiles"] =
                    AllowAuxiliaryNonImplementationFiles ? "true" : "false",
                ["createNewEpicSourceHash"] = Core.Prompts.Planning.CreateNewEpic.SourceHash,
                ["sectionMode"] = sections.ActiveSectionSourceHashes.Count == 0 ? "omitted" : "strict",
            };

            foreach ((string sectionName, string sourceHash) in sections.ActiveSectionSourceHashes)
            {
                inputs[$"section.{sectionName}.sourceHash"] = sourceHash;
            }

            return TransitionPromptPolicyIdentity.Create(
                "create-new-epic-prompt-owned-v1",
                inputs);
        }

        return TransitionPromptPolicyIdentity.Create(
            "legacy-implementation-first-composer-v1",
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["legacyImplementationFirstPromptPolicyHash"] =
                    RoadmapHash.Sha256(LegacyImplementationFirstPromptPolicy),
            });
    }
}
