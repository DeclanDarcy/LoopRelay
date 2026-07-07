using LoopRelay.Permissions.Models;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public static class ImplementationFirstPromptPolicyComposer
{
    public static string Compose(NonImplementationArtifactPolicyOptions artifactPolicy)
    {
        ArgumentNullException.ThrowIfNull(artifactPolicy);

        string hitlMode = artifactPolicy.AllowHitlRequestedNonImplementationFiles
            ? "The HITL-requested exception is enabled, but a non-implementation deliverable may be produced only when explicit HITL request evidence is captured for that deliverable."
            : "The HITL-requested exception is disabled; do not produce non-implementation deliverables at prompt time.";

        return string.Join(
            Environment.NewLine,
            "Repository growth is implementation-first: prefer code, tests, project files, runtime configuration, required generated artifacts, and sanctioned LoopRelay operational artifacts under .agents.",
            hitlMode,
            "Never invent autonomous documentation, documentation-centric milestones, theory-protection artifacts, freeze/certification/governance/authority documents, repository acceptance gates, or publication gates.",
            "This prompt policy controls generation guidance only; it does not disable post-execution non-implementation review.");
    }
}
