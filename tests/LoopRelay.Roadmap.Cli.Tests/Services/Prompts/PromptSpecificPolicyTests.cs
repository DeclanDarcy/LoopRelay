using LoopRelay.Permissions.Models.Policy;
using LoopRelay.Roadmap.Cli.Services.Prompts;

namespace LoopRelay.Roadmap.Cli.Tests.Services.Prompts;

public sealed class RealignEpicPromptPolicyTests
{
    [Fact]
    public void RealignEpic_uses_prompt_owned_sections_and_identity()
    {
        PromptSpecificPolicyAssertions.AssertPromptOwned(
            "RealignEpic",
            "# RealignEpic Implementation-First Guidance",
            "# RealignEpic Auxiliary Artifact Limits",
            "{realignEpicImplementationFirstGuidance}",
            "{realignEpicAuxiliaryArtifactLimits}",
            ".agents/epic.md",
            "realign-epic-prompt-owned-v1",
            "realignEpicSourceHash",
            "RealignEpicImplementationFirstGuidance",
            "RealignEpicAuxiliaryArtifactLimits");
    }
}

public sealed class ReimagineEpicPromptPolicyTests
{
    [Fact]
    public void ReimagineEpic_uses_prompt_owned_sections_and_identity()
    {
        PromptSpecificPolicyAssertions.AssertPromptOwned(
            "ReimagineEpic",
            "# ReimagineEpic Implementation-First Guidance",
            "# ReimagineEpic Auxiliary Artifact Limits",
            "{reimagineEpicImplementationFirstGuidance}",
            "{reimagineEpicAuxiliaryArtifactLimits}",
            ".agents/epic.md",
            "reimagine-epic-prompt-owned-v1",
            "reimagineEpicSourceHash",
            "ReimagineEpicImplementationFirstGuidance",
            "ReimagineEpicAuxiliaryArtifactLimits");
    }
}

public sealed class GenerateMilestoneDeepDivesForEpicPromptPolicyTests
{
    [Fact]
    public void GenerateMilestoneDeepDivesForEpic_uses_prompt_owned_sections_and_identity()
    {
        PromptSpecificPolicyAssertions.AssertPromptOwned(
            "GenerateMilestoneDeepDivesForEpic",
            "# GenerateMilestoneDeepDivesForEpic Implementation-First Guidance",
            "# GenerateMilestoneDeepDivesForEpic Auxiliary Artifact Limits",
            "{generateMilestoneDeepDivesForEpicImplementationFirstGuidance}",
            "{generateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits}",
            ".agents/specs/*.md",
            "generate-milestone-deep-dives-for-epic-prompt-owned-v1",
            "generateMilestoneDeepDivesForEpicSourceHash",
            "GenerateMilestoneDeepDivesForEpicImplementationFirstGuidance",
            "GenerateMilestoneDeepDivesForEpicAuxiliaryArtifactLimits");
    }
}

public sealed class SplitEpicPromptPolicyTests
{
    [Fact]
    public void SplitEpic_uses_prompt_owned_sections_and_identity()
    {
        PromptSpecificPolicyAssertions.AssertPromptOwned(
            "SplitEpic",
            "# SplitEpic Implementation-First Guidance",
            "# SplitEpic Auxiliary Artifact Limits",
            "{splitEpicImplementationFirstGuidance}",
            "{splitEpicAuxiliaryArtifactLimits}",
            ".agents/epic",
            "split-epic-prompt-owned-v1",
            "splitEpicSourceHash",
            "SplitEpicImplementationFirstGuidance",
            "SplitEpicAuxiliaryArtifactLimits");
    }
}

internal static class PromptSpecificPolicyAssertions
{
    public static void AssertPromptOwned(
        string runtimePromptName,
        string guidanceHeading,
        string limitsHeading,
        string guidancePlaceholder,
        string limitsPlaceholder,
        string primaryArtifact,
        string mode,
        string promptSourceHashKey,
        string guidanceSectionKey,
        string limitsSectionKey)
    {
        string strict = RoadmapPromptCatalog.RenderRuntime(
            runtimePromptName,
            "project context",
            SecondaryInput(runtimePromptName),
            Policy(allowAuxiliaryNonImplementationFiles: false));
        string allowed = RoadmapPromptCatalog.RenderRuntime(
            runtimePromptName,
            "project context",
            SecondaryInput(runtimePromptName),
            Policy(allowAuxiliaryNonImplementationFiles: true));
        var strictIdentity = Policy(allowAuxiliaryNonImplementationFiles: false).CreateIdentity(runtimePromptName);
        var allowedIdentity = Policy(allowAuxiliaryNonImplementationFiles: true).CreateIdentity(runtimePromptName);

        Assert.Contains(guidanceHeading, strict, StringComparison.Ordinal);
        Assert.Contains(limitsHeading, strict, StringComparison.Ordinal);
        Assert.Contains(primaryArtifact, strict, StringComparison.Ordinal);
        Assert.DoesNotContain(guidancePlaceholder, strict, StringComparison.Ordinal);
        Assert.DoesNotContain(limitsPlaceholder, strict, StringComparison.Ordinal);

        Assert.DoesNotContain(guidanceHeading, allowed, StringComparison.Ordinal);
        Assert.DoesNotContain(limitsHeading, allowed, StringComparison.Ordinal);
        Assert.Contains(primaryArtifact, allowed, StringComparison.Ordinal);
        Assert.DoesNotContain(guidancePlaceholder, allowed, StringComparison.Ordinal);
        Assert.DoesNotContain(limitsPlaceholder, allowed, StringComparison.Ordinal);

        Assert.Equal(mode, strictIdentity.Mode);
        Assert.Equal("strict", strictIdentity.Inputs["sectionMode"]);
        Assert.Contains(promptSourceHashKey, strictIdentity.Inputs.Keys);
        Assert.Contains($"section.{guidanceSectionKey}.sourceHash", strictIdentity.Inputs.Keys);
        Assert.Contains($"section.{limitsSectionKey}.sourceHash", strictIdentity.Inputs.Keys);

        Assert.Equal(mode, allowedIdentity.Mode);
        Assert.Equal("omitted", allowedIdentity.Inputs["sectionMode"]);
        Assert.DoesNotContain(
            allowedIdentity.Inputs.Keys,
            key => key.StartsWith("section.", StringComparison.Ordinal));
        Assert.NotEqual(strictIdentity.Hash, allowedIdentity.Hash);
    }

    private static RoadmapRuntimePromptPolicy Policy(bool allowAuxiliaryNonImplementationFiles) =>
        RoadmapRuntimePromptPolicy.FromArtifactPolicy(
            new NonImplementationArtifactPolicyOptions(
                AllowHitlRequestedNonImplementationFiles: false,
                AllowAuxiliaryNonImplementationFiles: allowAuxiliaryNonImplementationFiles));

    private static string SecondaryInput(string runtimePromptName) =>
        runtimePromptName switch
        {
            "RealignEpic" => "audit input",
            "ReimagineEpic" => "audit input",
            "SplitEpic" => "split proposal",
            _ => string.Empty,
        };
}
