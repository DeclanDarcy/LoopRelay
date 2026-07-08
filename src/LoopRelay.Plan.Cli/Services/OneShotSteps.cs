using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Permissions.Models;
using LoopRelay.Plan.Cli.Models;

namespace LoopRelay.Plan.Cli.Services;

/// <summary>
/// Factories for permission-scoped artifact operations in pipeline steps 6-10.
/// </summary>
internal static class OneShotSteps
{
    /// <summary>
    /// Pipeline step 6: allows every <c>.agents/specs/*.md</c> file plus <c>.agents/plan.md</c>, runs
    /// <see cref="CollectDetails"/>, and requires <c>.agents/details.md</c> to exist afterward.
    /// </summary>
    public static async Task<ArtifactOperationPlan> CollectDetailsAsync(PlanArtifacts artifacts)
    {
        List<string> allowedReads = (await artifacts.ListSpecsRelativeAsync()).ToList();
        allowedReads.Add(OrchestrationArtifactPaths.Plan);

        return new ArtifactOperationPlan(
            Label: "collect-details",
            Prompt: CollectDetails.Text,
            AllowedReads: allowedReads,
            AllowedReadGlobs: [new OperationPathGlob(".agents/specs", "*.md")],
            AllowedWrites: [OrchestrationArtifactPaths.Details],
            AllowedWriteGlobs: [],
            RequiredOutputs: [OrchestrationArtifactPaths.Details],
            RequiredOutputGlob: null,
            ChangedGuard: null,
            RequireChecklistInGlob: false);
    }

    /// <summary>
    /// Pipeline step 8: allows only <c>.agents/plan.md</c>, runs <see cref="ExtractMilestones"/>, requires the
    /// <c>.agents/milestones/m*.md</c> glob to be non-empty AND contain at least one strict checkbox across all
    /// matches (the false-closure guard — milestone files without trackable checkboxes would strand the main
    /// loop's epic-complete gate forever), and requires <c>plan.md</c> to have been rewritten (its milestone
    /// entries replaced with <c>(See ./…)</c> pointers) — an unchanged plan means the split did not happen.
    /// Keeps the rewritten plan (strictly, per the changed guard) plus every extracted milestone file.
    /// </summary>
    public static Task<ArtifactOperationPlan> ExtractMilestonesAsync(PlanArtifacts artifacts) =>
        Task.FromResult(new ArtifactOperationPlan(
            Label: "extract-milestones",
            Prompt: ExtractMilestones.Text,
            AllowedReads: [OrchestrationArtifactPaths.Plan],
            AllowedReadGlobs: [],
            AllowedWrites: [OrchestrationArtifactPaths.Plan],
            AllowedWriteGlobs: [new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)],
            RequiredOutputs: [],
            RequiredOutputGlob: new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern),
            ChangedGuard: OrchestrationArtifactPaths.Plan,
            RequireChecklistInGlob: true));

    /// <summary>
    /// Pipeline step 10: allows <c>.agents/details.md</c> plus every existing
    /// <c>.agents/milestones/m*.md</c>, runs <see cref="ExtractDetails"/>, and requires details to remain.
    /// </summary>
    public static async Task<ArtifactOperationPlan> ExtractDetailsAsync(PlanArtifacts artifacts)
    {
        List<string> allowedReads = [OrchestrationArtifactPaths.Details];
        allowedReads.AddRange(await artifacts.ListMilestonesRelativeAsync());

        return new ArtifactOperationPlan(
            Label: "extract-details",
            Prompt: ExtractDetails.Text,
            AllowedReads: allowedReads,
            AllowedReadGlobs: [new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)],
            AllowedWrites: [OrchestrationArtifactPaths.Details],
            AllowedWriteGlobs: [new OperationPathGlob(OrchestrationArtifactPaths.MilestonesDirectory, OrchestrationArtifactPaths.MilestoneSearchPattern)],
            RequiredOutputs: [OrchestrationArtifactPaths.Details],
            RequiredOutputGlob: null,
            ChangedGuard: null,
            RequireChecklistInGlob: false);
    }
}
