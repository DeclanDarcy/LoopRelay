using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Services;

public sealed record EvalPromptAsset(
    WorkflowTransitionIdentity Transition,
    string PromptIdentity,
    string PromptAssetName,
    string PromptTemplate,
    ProductIdentity PrimaryOutput,
    string PrimaryOutputPath,
    string SourceHash);

public static class EvalPromptAssetCatalog
{
    public static IReadOnlyList<EvalPromptAsset> Assets { get; } =
    [
        Asset(
            "CreateEvalDependencyInventory",
            "CreateEvalDependencyInventory.prompt",
            global::LoopRelay.Core.Prompts.Eval.CreateEvalDependencyInventory.Template,
            ProductIdentity.DependencyInventory,
            EvaluationArtifactPaths.DependencyInventory,
            global::LoopRelay.Core.Prompts.Eval.CreateEvalDependencyInventory.SourceHash),
        Asset(
            "CreateEvalHypothesisInventory",
            "CreateEvalHypothesisInventory.prompt",
            global::LoopRelay.Core.Prompts.Eval.CreateEvalHypothesisInventory.Template,
            ProductIdentity.HypothesisInventory,
            EvaluationArtifactPaths.HypothesisInventory,
            global::LoopRelay.Core.Prompts.Eval.CreateEvalHypothesisInventory.SourceHash),
        Asset(
            "CreateEvalArchitecturalCatalog",
            "CreateArchitecturalCatalog.prompt",
            global::LoopRelay.Core.Prompts.Eval.CreateArchitecturalCatalog.Template,
            ProductIdentity.ArchitecturalCatalog,
            EvaluationArtifactPaths.ArchitecturalCatalog,
            global::LoopRelay.Core.Prompts.Eval.CreateArchitecturalCatalog.SourceHash),
        Asset(
            "CreateEvalDag",
            "CreateEvalDag.prompt",
            global::LoopRelay.Core.Prompts.Eval.CreateEvalDag.Template,
            ProductIdentity.EvalDag,
            EvaluationArtifactPaths.EvalDag,
            global::LoopRelay.Core.Prompts.Eval.CreateEvalDag.SourceHash),
        Asset(
            "CreateNextEpicRoadmap",
            "CreateNextEpicRoadmap.prompt",
            global::LoopRelay.Core.Prompts.Eval.CreateNextEpicRoadmap.Template,
            ProductIdentity.NextEpicRoadmap,
            EvaluationArtifactPaths.NextEpicRoadmap,
            global::LoopRelay.Core.Prompts.Eval.CreateNextEpicRoadmap.SourceHash),
        Asset(
            "CreateNextEpicActiveEpic",
            "CreateNextEpicImplementationSpec.prompt",
            global::LoopRelay.Core.Prompts.Eval.CreateNextEpicImplementationSpec.Template,
            ProductIdentity.PreparedEpic,
            EvaluationArtifactPaths.PreparedEpic,
            global::LoopRelay.Core.Prompts.Eval.CreateNextEpicImplementationSpec.SourceHash),
        Asset(
            "RefreshEvalDependencyInventoryStatus",
            "UpdateDependencyInventory.prompt",
            global::LoopRelay.Core.Prompts.Eval.UpdateDependencyInventory.Template,
            ProductIdentity.DependencyInventory,
            EvaluationArtifactPaths.DependencyInventory,
            global::LoopRelay.Core.Prompts.Eval.UpdateDependencyInventory.SourceHash),
        Asset(
            "RefreshEvalHypothesisInventoryStatus",
            "UpdateHypothesisInventory.prompt",
            global::LoopRelay.Core.Prompts.Eval.UpdateHypothesisInventory.Template,
            ProductIdentity.HypothesisInventory,
            EvaluationArtifactPaths.HypothesisInventory,
            global::LoopRelay.Core.Prompts.Eval.UpdateHypothesisInventory.SourceHash),
        Asset(
            "RefreshNextEpicRoadmapStatus",
            "UpdateRoadmap.prompt",
            global::LoopRelay.Core.Prompts.Eval.UpdateRoadmap.Template,
            ProductIdentity.NextEpicRoadmap,
            EvaluationArtifactPaths.NextEpicRoadmap,
            global::LoopRelay.Core.Prompts.Eval.UpdateRoadmap.SourceHash),
    ];

    public static EvalPromptAsset GetByTransition(WorkflowTransitionIdentity transition) =>
        Assets.Single(asset => asset.Transition == transition);

    public static bool TryGetByTransition(
        WorkflowTransitionIdentity transition,
        out EvalPromptAsset asset)
    {
        EvalPromptAsset? match = Assets.FirstOrDefault(item => item.Transition == transition);
        asset = match!;
        return match is not null;
    }

    private static EvalPromptAsset Asset(
        string transition,
        string promptAssetName,
        string promptTemplate,
        ProductIdentity primaryOutput,
        string primaryOutputPath,
        string sourceHash)
    {
        string promptIdentity = Path.GetFileNameWithoutExtension(promptAssetName);
        return new EvalPromptAsset(
            new WorkflowTransitionIdentity(transition),
            promptIdentity,
            promptAssetName,
            promptTemplate,
            primaryOutput,
            primaryOutputPath,
            sourceHash);
    }
}
