using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Roadmap.Cli.Services.Prompts;

namespace LoopRelay.Roadmap.Cli.Tests.Services.TransitionState;

public sealed class RoadmapPromptPolicyIdentityTests
{
    public static TheoryData<string, string, string, string, string> PromptOwnedIdentityCases => new()
    {
        {
            "CreateNewEpic",
            "create-new-epic-prompt-owned-v1",
            "createNewEpicSourceHash",
            "CreateNewEpicImplementationFirstGuidance",
            "CreateNewEpicAuxiliaryArtifactLimits"
        },
        {
            "RealignEpic",
            "realign-epic-prompt-owned-v1",
            "realignEpicSourceHash",
            "RealignEpicImplementationFirstGuidance",
            "RealignEpicAuxiliaryArtifactLimits"
        },
        {
            "ReimagineEpic",
            "reimagine-epic-prompt-owned-v1",
            "reimagineEpicSourceHash",
            "ReimagineEpicImplementationFirstGuidance",
            "ReimagineEpicAuxiliaryArtifactLimits"
        },
        {
            "GenerateMilestoneDeepDivesForEpic",
            "generate-milestone-deep-dives-for-epic-prompt-owned-v1",
            "generateMilestoneDeepDivesForEpicSourceHash",
            "GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance",
            "GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits"
        },
        {
            "SplitEpic",
            "split-epic-prompt-owned-v1",
            "splitEpicSourceHash",
            "SplitEpicImplementationFirstGuidance",
            "SplitEpicAuxiliaryArtifactLimits"
        },
    };

    [Theory]
    [MemberData(nameof(PromptOwnedIdentityCases))]
    public void Prompt_owned_identity_records_prompt_and_active_section_source_hashes(
        string runtimePromptName,
        string mode,
        string promptSourceHashKey,
        string guidanceSectionKey,
        string limitsSectionKey)
    {
        var strict = Policy(
            allowAuxiliaryNonImplementationFiles: false,
            allowHitlRequestedNonImplementationFiles: false).CreateIdentity(runtimePromptName);
        var allowed = Policy(
            allowAuxiliaryNonImplementationFiles: true,
            allowHitlRequestedNonImplementationFiles: false).CreateIdentity(runtimePromptName);

        Assert.Equal(mode, strict.Mode);
        Assert.Equal("false", strict.Inputs["allowAuxiliaryNonImplementationFiles"]);
        Assert.Equal("strict", strict.Inputs["sectionMode"]);
        Assert.Contains(promptSourceHashKey, strict.Inputs.Keys);
        Assert.Contains($"section.{guidanceSectionKey}.sourceHash", strict.Inputs.Keys);
        Assert.Contains($"section.{limitsSectionKey}.sourceHash", strict.Inputs.Keys);

        Assert.Equal(mode, allowed.Mode);
        Assert.Equal("true", allowed.Inputs["allowAuxiliaryNonImplementationFiles"]);
        Assert.Equal("omitted", allowed.Inputs["sectionMode"]);
        Assert.Contains(promptSourceHashKey, allowed.Inputs.Keys);
        Assert.DoesNotContain(allowed.Inputs.Keys, key => key.StartsWith("section.", StringComparison.Ordinal));
        Assert.NotEqual(strict.Hash, allowed.Hash);
    }

    [Theory]
    [MemberData(nameof(PromptOwnedIdentityCases))]
    public void Prompt_owned_identity_is_not_affected_by_hitl_requested_policy(
        string runtimePromptName,
        string mode,
        string promptSourceHashKey,
        string guidanceSectionKey,
        string limitsSectionKey)
    {
        var withoutHitl = Policy(
            allowAuxiliaryNonImplementationFiles: false,
            allowHitlRequestedNonImplementationFiles: false).CreateIdentity(runtimePromptName);
        var withHitl = Policy(
            allowAuxiliaryNonImplementationFiles: false,
            allowHitlRequestedNonImplementationFiles: true).CreateIdentity(runtimePromptName);

        Assert.Equal(withoutHitl.Hash, withHitl.Hash);
        Assert.Equal(mode, withoutHitl.Mode);
        Assert.Equal(withoutHitl.Mode, withHitl.Mode);
        Assert.Contains(promptSourceHashKey, withoutHitl.Inputs.Keys);
        Assert.Contains($"section.{guidanceSectionKey}.sourceHash", withoutHitl.Inputs.Keys);
        Assert.Contains($"section.{limitsSectionKey}.sourceHash", withoutHitl.Inputs.Keys);
        Assert.Equal(withoutHitl.Inputs, withHitl.Inputs);
    }

    [Fact]
    public void Legacy_identity_still_records_legacy_composer_policy_hash()
    {
        var identity = Policy(
            allowAuxiliaryNonImplementationFiles: false,
            allowHitlRequestedNonImplementationFiles: false).CreateIdentity("SelectNextEpic");

        Assert.Equal("legacy-implementation-first-composer-v1", identity.Mode);
        Assert.Contains("legacyImplementationFirstPromptPolicyHash", identity.Inputs.Keys);
        Assert.DoesNotContain(identity.Inputs.Keys, key => key.StartsWith("section.", StringComparison.Ordinal));
    }

    private static RoadmapRuntimePromptPolicy Policy(
        bool allowAuxiliaryNonImplementationFiles,
        bool allowHitlRequestedNonImplementationFiles) =>
        RoadmapRuntimePromptPolicy.FromArtifactPolicy(
            new NonImplementationArtifactPolicyOptions(
                AllowHitlRequestedNonImplementationFiles: allowHitlRequestedNonImplementationFiles,
                AllowAuxiliaryNonImplementationFiles: allowAuxiliaryNonImplementationFiles));
}
