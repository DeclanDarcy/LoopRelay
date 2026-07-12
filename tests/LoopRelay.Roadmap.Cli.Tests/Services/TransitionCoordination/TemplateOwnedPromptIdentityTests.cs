using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Services.TransitionCoordination;
using Xunit;

namespace LoopRelay.Roadmap.Cli.Tests.Services.TransitionCoordination;

/// <summary>
/// With the prompt policy template-owned (M6), the template's build-time source hash is the
/// policy-complete prompt version. These tests pin the identity the transition journal stamps
/// into input snapshots, so a persisted roadmap transition stays tied to the prompt version
/// that rendered it (the retired composer-based identity's replacement).
/// </summary>
public sealed class TemplateOwnedPromptIdentityTests
{
    [Fact]
    public void Identity_carries_the_templates_source_hash_under_the_template_owned_mode()
    {
        TransitionPromptPolicyIdentity identity =
            RoadmapPromptTransitionRunner.TemplateOwnedPromptIdentity("CreateNewEpic");

        Assert.Equal("template-owned-v1", identity.Mode);
        Assert.Equal(
            LoopRelay.Core.Prompts.Planning.CreateNewEpic.SourceHash,
            identity.Inputs["promptSourceHash"]);
        Assert.NotEqual(TransitionPromptPolicyIdentity.None.Hash, identity.Hash);
    }

    [Fact]
    public void Different_templates_yield_different_prompt_policy_identities()
    {
        Assert.NotEqual(
            RoadmapPromptTransitionRunner.TemplateOwnedPromptIdentity("CreateNewEpic").Hash,
            RoadmapPromptTransitionRunner.TemplateOwnedPromptIdentity("SplitEpic").Hash);
    }

    [Fact]
    public void Every_runtime_prompt_resolves_a_template_source_hash()
    {
        string[] runtimePrompts =
        [
            "CreateRoadmapCompletionContext",
            "UpdateRoadmapCompletionContext",
            "SelectNextEpic",
            "EpicPreparationAudit",
            "RealignEpic",
            "ReimagineEpic",
            "CreateNewEpic",
            "SplitEpic",
            "GenerateMilestoneDeepDivesForEpic",
            "EvaluateEpicCompletionAndDrift",
            "SynthesizeCompletedEpic",
        ];

        foreach (string prompt in runtimePrompts)
        {
            TransitionPromptPolicyIdentity identity =
                RoadmapPromptTransitionRunner.TemplateOwnedPromptIdentity(prompt);
            Assert.Equal(64, identity.Inputs["promptSourceHash"].Length);
        }
    }
}
