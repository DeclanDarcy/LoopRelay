using LoopRelay.Core.Models.Identity;
using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration.Runtime;

namespace LoopRelay.Orchestration.Tests.Prompts;

public sealed class PromptPolicyCompositionTests
{
    [Fact]
    public void Completion_evaluation_prompt_embeds_the_certification_policy_matrix()
    {
        string template = global::LoopRelay.Core.Prompts.Planning.EvaluateEpicCompletionAndDrift.Template;

        Assert.Contains("Never pair Functionally Complete or Fully Complete with Continue Epic", template, StringComparison.Ordinal);
        Assert.Contains("Never pair Negative drift with Close Epic or", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Detail_refinement_preserves_the_existing_milestone_identity_set()
    {
        string template = global::LoopRelay.Core.Prompts.ExtractDetails.Template;

        Assert.Contains("Preserve the exact existing milestone file set", template, StringComparison.Ordinal);
        Assert.Contains("Do not create, rename, copy, split, merge, or delete milestone files", template, StringComparison.Ordinal);
    }

    private const string PolicyHeading = "## Implementation-First Prompt Policy";

    private static readonly IReadOnlyDictionary<string, string> PolicyConsumers =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GenerateSystemPromptForFirstExecutionAgent"] = GenerateSystemPromptForFirstExecutionAgent.Template,
            ["GenerateSystemPromptForNextExecutionAgent"] = GenerateSystemPromptForNextExecutionAgent.Template,
            ["WritePlan"] = WritePlan.Template,
            ["RevisePlan"] = RevisePlan.Template,
            ["SelectNextEpic"] = Core.Prompts.Planning.SelectNextEpic.Template,
            ["EpicPreparationAudit"] = Core.Prompts.Planning.EpicPreparationAudit.Template,
            ["CreateRoadmapCompletionContext"] = Core.Prompts.Planning.CreateRoadmapCompletionContext.Template,
            ["UpdateRoadmapCompletionContext"] = Core.Prompts.Planning.UpdateRoadmapCompletionContext.Template,
            ["EvaluateEpicCompletionAndDrift"] = Core.Prompts.Planning.EvaluateEpicCompletionAndDrift.Template,
            ["SynthesizeCompletedEpic"] = Core.Prompts.Planning.SynthesizeCompletedEpic.Template,
        };

    [Fact]
    public void SelectablePolicyProseIsNotDuplicatedIntoInvariantTemplates()
    {
        foreach ((string name, string template) in PolicyConsumers)
        {
            Assert.DoesNotContain(PolicyHeading, template, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Repository growth is implementation-first",
                template,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CanonicalComposerAddsTheResolvedProfileBeforePromptHashing()
    {
        var profile = new PromptPolicyProfile(
            new PromptPolicyProfileIdentity("prompt-policy-v2"),
            $"{PolicyHeading}\n\nRepository growth is implementation-first.");
        PromptComposition composition = new CanonicalPromptComposer().Compose(
            new PromptTemplateIdentity("template-a"),
            "template-hash",
            new PolicyIdentity("policy-a"),
            profile,
            ConsumedInputManifestIdentity.New(),
            [],
            new Dictionary<string, string>(),
            "Invariant template text.");

        Assert.Equal(profile.Identity, composition.PolicyProfile);
        Assert.StartsWith("Invariant template text.", composition.RenderedContent, StringComparison.Ordinal);
        Assert.EndsWith(profile.Content, composition.RenderedContent, StringComparison.Ordinal);
        Assert.Contains(PolicyHeading, composition.RenderedContent, StringComparison.Ordinal);
    }

    [Fact]
    public void DifferentPolicyProfilesProduceDifferentRenderedHashes()
    {
        var composer = new CanonicalPromptComposer();
        PromptComposition first = Compose(composer, "prompt-policy-v1", "Rule one.");
        PromptComposition second = Compose(composer, "prompt-policy-v2", "Rule two.");

        Assert.NotEqual(
            RenderedPromptFact.ComputeContentHash(first.RenderedContent),
            RenderedPromptFact.ComputeContentHash(second.RenderedContent));
    }

    [Fact]
    public void SynthesisTemplateRetainsItsNoRepositoryMutationResponseContract()
    {
        string template = Core.Prompts.Planning.SynthesizeCompletedEpic.Template;

        Assert.Contains("Do not modify the repository directly", template, StringComparison.Ordinal);
        Assert.Contains("trusted runtime owns persistence", template, StringComparison.Ordinal);
        Assert.Contains("Output only the Markdown document", template, StringComparison.Ordinal);
    }

    [Fact]
    public void RevisePlanRetainsTheOrchestrationPublicationBoundary()
    {
        string template = RevisePlan.Template;

        Assert.Contains("capability implementation and local verification occur before", template, StringComparison.Ordinal);
        Assert.Contains("Publication is not an implementation", template, StringComparison.Ordinal);
        Assert.Contains("absence of a callable publisher is not an implementation blocker", template, StringComparison.Ordinal);
    }

    private static PromptComposition Compose(CanonicalPromptComposer composer, string profileId, string rule) =>
        composer.Compose(
            new PromptTemplateIdentity("template-a"),
            "template-hash",
            new PolicyIdentity("policy-a"),
            new PromptPolicyProfile(new PromptPolicyProfileIdentity(profileId), rule),
            ConsumedInputManifestIdentity.New(),
            [],
            new Dictionary<string, string>(),
            "Invariant template text.");
}
