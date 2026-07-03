using CommandCenter.Core.Prompts;
using CommandCenter.Orchestration;

namespace CommandCenter.Plan.Cli;

/// <summary>
/// Factories for the <see cref="SandboxedStepPlan"/>s pipeline steps 6-10 run through
/// <see cref="SandboxedPromptStep"/>. Per-step seeding/gating is spelled out in the plan's Artifact Protocol
/// "Per-step seeding table"; only <see cref="CollectDetailsAsync"/> is implemented here (m4) — ExtractMilestones
/// and ExtractDetails are added in m5.
/// </summary>
internal static class OneShotSteps
{
    /// <summary>
    /// Pipeline step 6: seeds every <c>.agents/specs/*.md</c> file plus <c>.agents/plan.md</c>, runs
    /// <see cref="CollectDetails"/>, requires <c>.agents/details.md</c> to exist in the sandbox afterward (no
    /// changed guard — details.md is not seeded, so plain existence is a valid gate), and copies it back.
    /// </summary>
    public static async Task<SandboxedStepPlan> CollectDetailsAsync(PlanArtifacts artifacts)
    {
        List<string> seeds = (await artifacts.ListSpecsRelativeAsync()).ToList();
        seeds.Add(OrchestrationArtifactPaths.Plan);

        return new SandboxedStepPlan(
            Label: "collect-details",
            Prompt: CollectDetails.Text,
            Seeds: seeds,
            RequiredOutputs: [OrchestrationArtifactPaths.Details],
            RequiredOutputGlob: null,
            ChangedGuard: null,
            CopyBackFiles: [OrchestrationArtifactPaths.Details],
            CopyBackGlob: null);
    }
}
