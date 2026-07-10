using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class CanonicalPromptAssetCatalogTests
{
    [Fact]
    public void Catalog_registers_generated_prompt_assets_with_source_hashes()
    {
        Assert.All(CanonicalPromptAssetCatalog.Assets, asset =>
        {
            Assert.False(string.IsNullOrWhiteSpace(asset.PromptIdentity));
            Assert.EndsWith(".prompt", asset.PromptAssetName, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(asset.PromptTemplate));
            Assert.Matches("^[a-f0-9]{64}$", asset.SourceHash);
        });
    }

    [Theory]
    [InlineData("BootstrapRoadmapCompletionContext", "Planning/CreateRoadmapCompletionContext.prompt")]
    [InlineData("CreateNewEpic", "Planning/CreateNewEpic.prompt")]
    [InlineData("GenerateMilestoneDeepDivesForEpic", "Planning/GenerateMilestoneDeepDivesForEpic.prompt")]
    [InlineData("WritePlan", "WritePlan.prompt")]
    [InlineData("GenerateAdversarialProjection", "Projections/ProjectionForAdversarialPlanReview.prompt")]
    [InlineData("RunAdversarialReview", "AdversarialPlanReview.prompt")]
    [InlineData("ReviewAndRevisePlan", "RevisePlan.prompt")]
    [InlineData("CollectDetails", "CollectDetails.prompt")]
    [InlineData("ExtractDetails", "ExtractDetails.prompt")]
    [InlineData("ExtractMilestones", "ExtractMilestones.prompt")]
    [InlineData("GenerateHandoff", "GenerateHandoff.prompt")]
    [InlineData("UpdateOperationalContext", "UpdateOperationalContext.prompt")]
    public void Catalog_maps_canonical_prompt_identities_to_generated_assets(
        string promptIdentity,
        string promptAssetName)
    {
        CanonicalPromptAsset asset = CanonicalPromptAssetCatalog.GetByPromptIdentity(promptIdentity);

        Assert.Equal(promptAssetName, asset.PromptAssetName);
    }

    [Fact]
    public void Plan_traditional_and_execute_definitions_use_registered_prompt_assets_where_available()
    {
        WorkflowDefinition traditional = CanonicalWorkflowDefinitionSketches.CreateTraditionalRoadmap();
        WorkflowDefinition plan = CanonicalWorkflowDefinitionSketches.CreatePlan();
        WorkflowDefinition execute = CanonicalWorkflowDefinitionSketches.CreateExecute();

        AssertRegistered(traditional, "CreateEpic");
        AssertRegistered(traditional, "GenerateMilestoneDeepDivesForEpic");
        AssertRegistered(plan, "WriteExecutablePlan");
        AssertRegistered(plan, "CollectExecutionDetails");
        AssertRegistered(plan, "RunAdversarialReview");
        AssertRegistered(plan, "RevisePlan");
        AssertRegistered(plan, "GenerateExecutionMilestones");
        AssertRegistered(plan, "RefineExecutionDetails");
        AssertRegistered(execute, "GenerateHandoff");
        AssertRegistered(execute, "UpdateOperationalContext");
    }

    private static void AssertRegistered(
        WorkflowDefinition workflow,
        string transitionIdentity)
    {
        WorkflowTransitionDefinition transition = workflow.Transitions
            .Single(item => item.Identity == new WorkflowTransitionIdentity(transitionIdentity));

        Assert.True(
            CanonicalPromptAssetCatalog.HasAssetFor(transition),
            $"Expected {workflow.Identity}.{transitionIdentity} prompt `{transition.PromptIdentity}` to have a generated prompt asset.");
    }
}
