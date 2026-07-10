using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class EvaluationArtifactPathsTests
{
    [Fact]
    public void Evaluation_paths_match_the_canonical_eval_roadmap_contract()
    {
        Assert.Equal(".agents/evals", EvaluationArtifactPaths.InputDirectory);
        Assert.Equal(".agents/selected-evaluation.md", EvaluationArtifactPaths.SelectedEvaluation);
        Assert.Equal(".agents/eval-dependency-inventory.md", EvaluationArtifactPaths.DependencyInventory);
        Assert.Equal(".agents/eval-hypothesis-inventory.md", EvaluationArtifactPaths.HypothesisInventory);
        Assert.Equal(".agents/eval-architectural-catalog.md", EvaluationArtifactPaths.ArchitecturalCatalog);
        Assert.Equal(".agents/eval-dag.md", EvaluationArtifactPaths.EvalDag);
        Assert.Equal(".agents/next-epic-roadmap.md", EvaluationArtifactPaths.NextEpicRoadmap);
        Assert.Equal(".agents/epic.md", EvaluationArtifactPaths.PreparedEpic);
        Assert.Equal(".agents/specs", EvaluationArtifactPaths.MilestoneSpecificationDirectory);
        Assert.Equal("*.md", EvaluationArtifactPaths.MilestoneSpecificationPattern);
        Assert.Equal(".agents/evidence/eval", EvaluationArtifactPaths.EvidenceDirectory);
    }

    [Fact]
    public void Eval_prompt_asset_catalog_registers_generated_prompt_assets_and_outputs()
    {
        Assert.Equal(9, EvalPromptAssetCatalog.Assets.Count);
        Assert.All(EvalPromptAssetCatalog.Assets, asset =>
        {
            Assert.EndsWith(".prompt", asset.PromptAssetName, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(asset.SourceHash));
            Assert.Matches("^[a-f0-9]{64}$", asset.SourceHash);
            Assert.False(string.IsNullOrWhiteSpace(asset.PromptTemplate));
        });

        AssertRegistered(
            "CreateEvalDependencyInventory",
            "CreateEvalDependencyInventory",
            ProductIdentity.DependencyInventory,
            EvaluationArtifactPaths.DependencyInventory);
        AssertRegistered(
            "CreateEvalHypothesisInventory",
            "CreateEvalHypothesisInventory",
            ProductIdentity.HypothesisInventory,
            EvaluationArtifactPaths.HypothesisInventory);
        AssertRegistered(
            "CreateEvalArchitecturalCatalog",
            "CreateArchitecturalCatalog",
            ProductIdentity.ArchitecturalCatalog,
            EvaluationArtifactPaths.ArchitecturalCatalog);
        AssertRegistered(
            "CreateEvalDag",
            "CreateEvalDag",
            ProductIdentity.EvalDag,
            EvaluationArtifactPaths.EvalDag);
        AssertRegistered(
            "CreateNextEpicRoadmap",
            "CreateNextEpicRoadmap",
            ProductIdentity.NextEpicRoadmap,
            EvaluationArtifactPaths.NextEpicRoadmap);
        AssertRegistered(
            "CreateNextEpicActiveEpic",
            "CreateNextEpicImplementationSpec",
            ProductIdentity.PreparedEpic,
            EvaluationArtifactPaths.PreparedEpic);
        AssertRegistered(
            "RefreshEvalDependencyInventoryStatus",
            "UpdateDependencyInventory",
            ProductIdentity.DependencyInventory,
            EvaluationArtifactPaths.DependencyInventory);
        AssertRegistered(
            "RefreshEvalHypothesisInventoryStatus",
            "UpdateHypothesisInventory",
            ProductIdentity.HypothesisInventory,
            EvaluationArtifactPaths.HypothesisInventory);
        AssertRegistered(
            "RefreshNextEpicRoadmapStatus",
            "UpdateRoadmap",
            ProductIdentity.NextEpicRoadmap,
            EvaluationArtifactPaths.NextEpicRoadmap);
    }

    [Fact]
    public void EvalRoadmap_definition_uses_registered_eval_prompt_assets()
    {
        WorkflowDefinition workflow = CanonicalWorkflowDefinitionSketches.CreateEvalRoadmap();

        foreach (EvalPromptAsset asset in EvalPromptAssetCatalog.Assets)
        {
            WorkflowTransitionDefinition transition = workflow.Transitions.Single(item => item.Identity == asset.Transition);
            Assert.Equal(asset.PromptIdentity, transition.PromptIdentity);
            Assert.Contains(transition.ProducedProducts, product => product.Identity == asset.PrimaryOutput);
        }
    }

    private static void AssertRegistered(
        string transition,
        string promptIdentity,
        ProductIdentity output,
        string outputPath)
    {
        EvalPromptAsset asset = EvalPromptAssetCatalog.GetByTransition(new WorkflowTransitionIdentity(transition));
        Assert.Equal(promptIdentity, asset.PromptIdentity);
        Assert.Equal(output, asset.PrimaryOutput);
        Assert.Equal(outputPath, asset.PrimaryOutputPath);
        Assert.False(string.IsNullOrWhiteSpace(asset.PromptTemplate));
    }
}
