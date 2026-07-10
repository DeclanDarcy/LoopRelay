using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class PlanScopedArtifactOperationCatalogTests
{
    [Fact]
    public void Catalog_declares_legacy_permissioned_artifact_operations_as_scoped_postures()
    {
        Assert.Equal(
            [
                "CollectExecutionDetails",
                "GenerateExecutionMilestones",
                "RefineExecutionDetails",
            ],
            PlanScopedArtifactOperationCatalog.All.Select(operation => operation.Transition.Value));

        PlanScopedArtifactOperationSpec collect = Get("CollectExecutionDetails");
        Assert.Equal("CollectDetails", collect.PromptIdentity);
        Assert.Equal("collect-details", collect.Label);
        Assert.Equal([OrchestrationArtifactPaths.Plan], collect.AllowedReads);
        AssertGlob(Assert.Single(collect.AllowedReadGlobs), OrchestrationArtifactPaths.SpecsDirectory, "*.md");
        Assert.Equal([OrchestrationArtifactPaths.Details], collect.AllowedWrites);
        Assert.Empty(collect.AllowedWriteGlobs);
        Assert.Equal([OrchestrationArtifactPaths.Details], collect.RequiredOutputs);
        Assert.Null(collect.RequiredOutputGlob);
        Assert.Null(collect.ChangedGuard);
        Assert.False(collect.RequireChecklistInGlob);

        PlanScopedArtifactOperationSpec milestones = Get("GenerateExecutionMilestones");
        Assert.Equal("ExtractMilestones", milestones.PromptIdentity);
        Assert.Equal("extract-milestones", milestones.Label);
        Assert.Equal([OrchestrationArtifactPaths.Plan], milestones.AllowedReads);
        Assert.Empty(milestones.AllowedReadGlobs);
        Assert.Equal([OrchestrationArtifactPaths.Plan], milestones.AllowedWrites);
        AssertGlob(
            Assert.Single(milestones.AllowedWriteGlobs),
            OrchestrationArtifactPaths.MilestonesDirectory,
            OrchestrationArtifactPaths.MilestoneSearchPattern);
        Assert.Empty(milestones.RequiredOutputs);
        AssertGlob(
            milestones.RequiredOutputGlob!,
            OrchestrationArtifactPaths.MilestonesDirectory,
            OrchestrationArtifactPaths.MilestoneSearchPattern);
        Assert.Equal(OrchestrationArtifactPaths.Plan, milestones.ChangedGuard);
        Assert.True(milestones.RequireChecklistInGlob);

        PlanScopedArtifactOperationSpec refine = Get("RefineExecutionDetails");
        Assert.Equal("ExtractDetails", refine.PromptIdentity);
        Assert.Equal("extract-details", refine.Label);
        Assert.Equal([OrchestrationArtifactPaths.Details], refine.AllowedReads);
        AssertGlob(
            Assert.Single(refine.AllowedReadGlobs),
            OrchestrationArtifactPaths.MilestonesDirectory,
            OrchestrationArtifactPaths.MilestoneSearchPattern);
        Assert.Equal([OrchestrationArtifactPaths.Details], refine.AllowedWrites);
        AssertGlob(
            Assert.Single(refine.AllowedWriteGlobs),
            OrchestrationArtifactPaths.MilestonesDirectory,
            OrchestrationArtifactPaths.MilestoneSearchPattern);
        Assert.Equal([OrchestrationArtifactPaths.Details], refine.RequiredOutputs);
        Assert.Null(refine.RequiredOutputGlob);
        Assert.Null(refine.ChangedGuard);
        Assert.False(refine.RequireChecklistInGlob);
    }

    [Fact]
    public void Catalog_rejects_non_permissioned_plan_transition()
    {
        Assert.False(PlanScopedArtifactOperationCatalog.Supports(new WorkflowTransitionIdentity("WriteExecutablePlan")));
    }

    private static PlanScopedArtifactOperationSpec Get(string transition) =>
        PlanScopedArtifactOperationCatalog.Get(new WorkflowTransitionIdentity(transition));

    private static void AssertGlob(OperationPathGlob glob, string directory, string pattern)
    {
        Assert.Equal(directory, glob.Directory);
        Assert.Equal(pattern, glob.Pattern);
    }
}
