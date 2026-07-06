using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration;

namespace LoopRelay.Plan.Cli;

/// <summary>
/// Factories for the <see cref="SandboxedStepPlan"/>s pipeline steps 6-10 run through
/// <see cref="SandboxedPromptStep"/>. Per-step seeding/gating is spelled out in the plan's Artifact Protocol
/// "Per-step seeding table".
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

    /// <summary>
    /// Pipeline step 8: seeds only <c>.agents/plan.md</c>, runs <see cref="ExtractMilestones"/>, requires the
    /// sandbox <c>milestones/m*.md</c> glob to be non-empty AND contain at least one strict checkbox across all
    /// matches (the false-closure guard — milestone files without trackable checkboxes would strand the main
    /// loop's epic-complete gate forever), and requires <c>plan.md</c> to have been rewritten (its milestone
    /// entries replaced with <c>(See ./…)</c> pointers) — an unchanged plan means the split did not happen.
    /// Copies back the rewritten plan (strictly, per the changed guard) plus every extracted milestone file.
    /// </summary>
    public static Task<SandboxedStepPlan> ExtractMilestonesAsync(PlanArtifacts artifacts) =>
        Task.FromResult(new SandboxedStepPlan(
            Label: "extract-milestones",
            Prompt: ExtractMilestones.Text,
            Seeds: [OrchestrationArtifactPaths.Plan],
            RequiredOutputs: [],
            RequiredOutputGlob: (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            ChangedGuard: OrchestrationArtifactPaths.Plan,
            CopyBackFiles: [OrchestrationArtifactPaths.Plan],
            CopyBackGlob: (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            RequireChecklistInGlob: true));

    /// <summary>
    /// Pipeline step 10: seeds <c>.agents/details.md</c> plus every existing <c>.agents/milestones/m*.md</c>,
    /// runs <see cref="ExtractDetails"/>, requires <c>details.md</c> to exist in the sandbox afterward (no
    /// changed guard — a no-op is legitimate when details are already universally relevant), and copies back
    /// details.md plus every milestone file (existence-guarded — a file the agent leaves untouched or deletes
    /// in its sandbox is not an error).
    /// </summary>
    public static async Task<SandboxedStepPlan> ExtractDetailsAsync(PlanArtifacts artifacts)
    {
        List<string> seeds = [OrchestrationArtifactPaths.Details];
        seeds.AddRange(await artifacts.ListMilestonesRelativeAsync());

        return new SandboxedStepPlan(
            Label: "extract-details",
            Prompt: ExtractDetails.Text,
            Seeds: seeds,
            RequiredOutputs: [OrchestrationArtifactPaths.Details],
            RequiredOutputGlob: null,
            ChangedGuard: null,
            CopyBackFiles: [OrchestrationArtifactPaths.Details],
            CopyBackGlob: (OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern));
    }
}
