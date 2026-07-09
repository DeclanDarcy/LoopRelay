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
        switch (runtimePromptName)
        {
            case "CreateNewEpic":
                return CreatePromptOwnedIdentity(
                    "create-new-epic-prompt-owned-v1",
                    "createNewEpicSourceHash",
                    Core.Prompts.Planning.CreateNewEpic.SourceHash,
                    CreateNewEpicPromptSections.ForAuxiliaryArtifactPolicy(
                            AllowAuxiliaryNonImplementationFiles)
                        .ActiveSectionSourceHashes);
            case "RealignEpic":
                return CreatePromptOwnedIdentity(
                    "realign-epic-prompt-owned-v1",
                    "realignEpicSourceHash",
                    Core.Prompts.Planning.RealignEpic.SourceHash,
                    RealignEpicPromptSections.ForAuxiliaryArtifactPolicy(
                            AllowAuxiliaryNonImplementationFiles)
                        .ActiveSectionSourceHashes);
            case "ReimagineEpic":
                return CreatePromptOwnedIdentity(
                    "reimagine-epic-prompt-owned-v1",
                    "reimagineEpicSourceHash",
                    Core.Prompts.Planning.ReimagineEpic.SourceHash,
                    ReimagineEpicPromptSections.ForAuxiliaryArtifactPolicy(
                            AllowAuxiliaryNonImplementationFiles)
                        .ActiveSectionSourceHashes);
            case "GenerateMilestoneDeepDivesForEpic":
                return CreatePromptOwnedIdentity(
                    "generate-milestone-deep-dives-for-epic-prompt-owned-v1",
                    "generateMilestoneDeepDivesForEpicSourceHash",
                    Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.SourceHash,
                    GenerateMilestoneDeepDivesForEpicPromptSections.ForAuxiliaryArtifactPolicy(
                            AllowAuxiliaryNonImplementationFiles)
                        .ActiveSectionSourceHashes);
            case "SplitEpic":
                return CreatePromptOwnedIdentity(
                    "split-epic-prompt-owned-v1",
                    "splitEpicSourceHash",
                    Core.Prompts.Planning.SplitEpic.SourceHash,
                    SplitEpicPromptSections.ForAuxiliaryArtifactPolicy(
                            AllowAuxiliaryNonImplementationFiles)
                        .ActiveSectionSourceHashes);
        }

        return TransitionPromptPolicyIdentity.Create(
            "legacy-implementation-first-composer-v1",
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["legacyImplementationFirstPromptPolicyHash"] =
                    RoadmapHash.Sha256(LegacyImplementationFirstPromptPolicy),
            });
    }

    private TransitionPromptPolicyIdentity CreatePromptOwnedIdentity(
        string mode,
        string promptSourceHashKey,
        string promptSourceHash,
        IReadOnlyDictionary<string, string> activeSectionSourceHashes)
    {
        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["allowAuxiliaryNonImplementationFiles"] =
                AllowAuxiliaryNonImplementationFiles ? "true" : "false",
            [promptSourceHashKey] = promptSourceHash,
            ["sectionMode"] = activeSectionSourceHashes.Count == 0 ? "omitted" : "strict",
        };

        foreach ((string sectionName, string sourceHash) in activeSectionSourceHashes)
        {
            inputs[$"section.{sectionName}.sourceHash"] = sourceHash;
        }

        return TransitionPromptPolicyIdentity.Create(mode, inputs);
    }
}
