using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration.Services.Hitl;

namespace LoopRelay.Orchestration.Tests.Prompts;

/// <summary>
/// M6 owner ruling: the implementation-first prompt policy is template-owned and unconditional —
/// no composer appends it after rendering, so the generated SourceHash of each template is the
/// policy-complete prompt version. These tests pin the promoted policy text into the templates
/// so it cannot silently fall out (which would resurrect the severed-wire failure mode the
/// ruling closed).
/// </summary>
public sealed class TemplateOwnedPromptPolicyTests
{
    private const string SectionHeading = "## Implementation-First Prompt Policy";

    // The complete canonical section, sentence for sentence — the single source the twelve
    // policy-owning templates are pinned against, so no promoted sentence can silently fall out
    // of any one template.
    private static readonly string CanonicalSection = string.Join(
        "\n",
        SectionHeading,
        string.Empty,
        "Repository growth is implementation-first: prefer code, tests, project files, runtime configuration, required generated artifacts, and sanctioned LoopRelay operational artifacts under .agents.",
        "Non-implementation deliverables require a structured explicit HITL request captured for the exact deliverable; without one, do not produce them at prompt time.",
        "Never infer the HITL exception from plan prose, file names, or agent-authored rationale.",
        $"When explicit HITL-requested non-implementation deliverables must be preserved, use the stable `{ExplicitHitlNonImplementationRequestCaptureService.SectionHeading}` section and table marker.",
        "Never invent autonomous documentation, documentation-centric milestones, freeze/certification/governance/authority documents, repository acceptance gates, publication gates, or theory-protection artifacts.",
        "Do not add Architecture Tests, Golden Tests, or theory-protection artifacts unless they are backed by executable evaluation or explicitly requested by HITL evidence.",
        "This prompt policy controls generation guidance only; it does not disable post-execution non-implementation review.");

    // Every template whose composed prompt previously received the appended policy section now
    // carries the section in its hashed source.
    private static readonly IReadOnlyDictionary<string, string> PolicyOwningTemplates =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GenerateSystemPromptForFirstExecutionAgent"] = GenerateSystemPromptForFirstExecutionAgent.Template,
            ["GenerateSystemPromptForNextExecutionAgent"] = GenerateSystemPromptForNextExecutionAgent.Template,
            ["ContinueExecution"] = ContinueExecution.Template,
            ["StartExecution"] = StartExecution.Template,
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
    public void Policy_owning_templates_carry_the_complete_implementation_first_section()
    {
        foreach ((string name, string template) in PolicyOwningTemplates)
        {
            // Line-ending-normalized so the pin is about content, not platform bytes. The HITL
            // capture marker inside CanonicalSection is built from the capture service's
            // constant, so the section and the service cannot drift apart.
            Assert.True(
                template.ReplaceLineEndings("\n").Contains(CanonicalSection, StringComparison.Ordinal),
                $"`{name}` template does not carry the complete canonical implementation-first policy section.");
        }
    }

    [Fact]
    public void Execution_seed_templates_have_no_unfilled_instruction_hole()
    {
        // The former {artifactCreationInstructions} hole (production passed the literal "TODO")
        // was replaced by the embedded policy section.
        Assert.DoesNotContain("{artifactCreationInstructions}", ContinueExecution.Template, StringComparison.Ordinal);
        Assert.DoesNotContain("{artifactCreationInstructions}", StartExecution.Template, StringComparison.Ordinal);
        Assert.DoesNotContain("TODO", ContinueExecution.Template, StringComparison.Ordinal);
    }

    [Fact]
    public void Epic_templates_own_their_guidance_and_limits_sections()
    {
        // The five epic templates own the guidance/limits sections the retired Roadmap catalog
        // used to inject into their holes — and which the unified path previously left as raw
        // unfilled `{...}` braces in agent-bound prompts.
        var templates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CreateNewEpic"] = Core.Prompts.Planning.CreateNewEpic.Template,
            ["RealignEpic"] = Core.Prompts.Planning.RealignEpic.Template,
            ["ReimagineEpic"] = Core.Prompts.Planning.ReimagineEpic.Template,
            ["SplitEpic"] = Core.Prompts.Planning.SplitEpic.Template,
            ["GenerateMilestoneDeepDivesForEpic"] = Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.Template,
        };

        foreach ((string name, string template) in templates)
        {
            Assert.True(
                template.Contains($"# {name} Implementation-First Guidance", StringComparison.Ordinal),
                $"`{name}` template is missing its implementation-first guidance section.");
            Assert.True(
                template.Contains($"# {name} Auxiliary Artifact Limits", StringComparison.Ordinal),
                $"`{name}` template is missing its auxiliary artifact limits section.");
            Assert.DoesNotContain("ImplementationFirstGuidance}", template, StringComparison.Ordinal);
            Assert.DoesNotContain("AuxiliaryArtifactLimits}", template, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Adversarial_plan_review_template_owns_the_former_inline_instruction_literals()
    {
        string template = AdversarialPlanReview.Template;

        // The seven blocks the renderer used to inject at {p1}..{p7} are template-owned now.
        Assert.Contains("A plan is not safe because it is coherent", template, StringComparison.Ordinal);
        Assert.Contains("treat implementation-first fidelity as an explicit attack vector", template, StringComparison.Ordinal);
        Assert.Contains("- Implementation-first fidelity: Does the plan advance implemented capability", template, StringComparison.Ordinal);
        Assert.Contains("If no projection rule is violated", template, StringComparison.Ordinal);
        Assert.Contains("Always include implementation-first false-closure modes", template, StringComparison.Ordinal);
        Assert.Contains("Corrections should move risk back into implementation-bearing plan content", template, StringComparison.Ordinal);
        Assert.Contains("- no realistic implementation-first false-success path remains", template, StringComparison.Ordinal);
        for (int hole = 1; hole <= 7; hole++)
        {
            Assert.DoesNotContain($"{{p{hole}}}", template, StringComparison.Ordinal);
        }
    }
}
