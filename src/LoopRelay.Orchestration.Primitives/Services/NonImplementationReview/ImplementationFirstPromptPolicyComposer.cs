using LoopRelay.Permissions.Models;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public static class ImplementationFirstPromptPolicyComposer
{
    public const string SectionHeading = "## Implementation-First Prompt Policy";

    public static string Compose(NonImplementationArtifactPolicyOptions artifactPolicy)
    {
        ArgumentNullException.ThrowIfNull(artifactPolicy);

        string hitlMode = artifactPolicy.AllowHitlRequestedNonImplementationFiles
            ? "HITL-request-enabled mode is active, but non-implementation deliverables still require explicit HITL request evidence captured for the exact deliverable."
            : "The HITL-requested exception is disabled; do not produce non-implementation deliverables at prompt time.";

        return string.Join(
            Environment.NewLine,
            "Repository growth is implementation-first: prefer code, tests, project files, runtime configuration, required generated artifacts, and sanctioned LoopRelay operational artifacts under .agents.",
            hitlMode,
            "The HITL-requested documentation exception applies only when the setting is enabled and a structured explicit HITL request has been captured; never infer the exception from plan prose, file names, or agent-authored rationale.",
            $"When explicit HITL-requested non-implementation deliverables must be preserved, use the stable `{ExplicitHitlNonImplementationRequestCaptureService.SectionHeading}` section and table marker.",
            "Never invent autonomous documentation, documentation-centric milestones, freeze/certification/governance/authority documents, repository acceptance gates, publication gates, or theory-protection artifacts.",
            "Do not add Architecture Tests, Golden Tests, or theory-protection artifacts unless they are backed by executable evaluation or explicitly requested by HITL evidence.",
            "This prompt policy controls generation guidance only; it does not disable post-execution non-implementation review.");
    }

    public static string ComposeDefault() => Compose(NonImplementationArtifactPolicyOptions.Default);

    public static string RenderSection(string promptPolicy)
    {
        if (string.IsNullOrWhiteSpace(promptPolicy))
        {
            throw new ArgumentException("Prompt policy must not be empty.", nameof(promptPolicy));
        }

        return string.Join(Environment.NewLine, SectionHeading, string.Empty, promptPolicy.Trim());
    }

    public static string AppendPromptPolicy(string prompt, string promptPolicy)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        return string.Join(
            Environment.NewLine,
            prompt.TrimEnd(),
            string.Empty,
            RenderSection(promptPolicy));
    }
}
