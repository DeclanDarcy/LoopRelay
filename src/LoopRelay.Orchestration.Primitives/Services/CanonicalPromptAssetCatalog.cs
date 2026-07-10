using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Services;

public sealed record CanonicalPromptAsset(
    string PromptIdentity,
    string PromptAssetName,
    string PromptTemplate,
    string SourceHash);

public static class CanonicalPromptAssetCatalog
{
    public static IReadOnlyList<CanonicalPromptAsset> Assets { get; } =
    [
        Asset(
            "BootstrapRoadmapCompletionContext",
            "Planning/CreateRoadmapCompletionContext.prompt",
            global::LoopRelay.Core.Prompts.Planning.CreateRoadmapCompletionContext.Template,
            global::LoopRelay.Core.Prompts.Planning.CreateRoadmapCompletionContext.SourceHash),
        Asset(
            "UpdateRoadmapCompletionContext",
            "Planning/UpdateRoadmapCompletionContext.prompt",
            global::LoopRelay.Core.Prompts.Planning.UpdateRoadmapCompletionContext.Template,
            global::LoopRelay.Core.Prompts.Planning.UpdateRoadmapCompletionContext.SourceHash),
        Asset(
            "SelectStrategicInitiative",
            "Planning/SelectNextEpic.prompt",
            global::LoopRelay.Core.Prompts.Planning.SelectNextEpic.Template,
            global::LoopRelay.Core.Prompts.Planning.SelectNextEpic.SourceHash),
        Asset(
            "AuditExistingEpic",
            "Planning/EpicPreparationAudit.prompt",
            global::LoopRelay.Core.Prompts.Planning.EpicPreparationAudit.Template,
            global::LoopRelay.Core.Prompts.Planning.EpicPreparationAudit.SourceHash),
        Asset(
            "CreateNewEpic",
            "Planning/CreateNewEpic.prompt",
            global::LoopRelay.Core.Prompts.Planning.CreateNewEpic.Template,
            global::LoopRelay.Core.Prompts.Planning.CreateNewEpic.SourceHash),
        Asset(
            "SplitEpic",
            "Planning/SplitEpic.prompt",
            global::LoopRelay.Core.Prompts.Planning.SplitEpic.Template,
            global::LoopRelay.Core.Prompts.Planning.SplitEpic.SourceHash),
        Asset(
            "RealignEpic",
            "Planning/RealignEpic.prompt",
            global::LoopRelay.Core.Prompts.Planning.RealignEpic.Template,
            global::LoopRelay.Core.Prompts.Planning.RealignEpic.SourceHash),
        Asset(
            "ReimagineEpic",
            "Planning/ReimagineEpic.prompt",
            global::LoopRelay.Core.Prompts.Planning.ReimagineEpic.Template,
            global::LoopRelay.Core.Prompts.Planning.ReimagineEpic.SourceHash),
        Asset(
            "GenerateMilestoneDeepDivesForEpic",
            "Planning/GenerateMilestoneDeepDivesForEpic.prompt",
            global::LoopRelay.Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.Template,
            global::LoopRelay.Core.Prompts.Planning.GenerateMilestoneDeepDivesForEpic.SourceHash),
        Asset(
            "WritePlan",
            "WritePlan.prompt",
            global::LoopRelay.Core.Prompts.WritePlan.Template,
            global::LoopRelay.Core.Prompts.WritePlan.SourceHash),
        Asset(
            "GenerateAdversarialProjection",
            "Projections/ProjectionForAdversarialPlanReview.prompt",
            global::LoopRelay.Core.Prompts.Projections.ProjectionForAdversarialPlanReview.Template,
            global::LoopRelay.Core.Prompts.Projections.ProjectionForAdversarialPlanReview.SourceHash),
        Asset(
            "RunAdversarialReview",
            "AdversarialPlanReview.prompt",
            global::LoopRelay.Core.Prompts.AdversarialPlanReview.Template,
            global::LoopRelay.Core.Prompts.AdversarialPlanReview.SourceHash),
        Asset(
            "ReviewAndRevisePlan",
            "RevisePlan.prompt",
            global::LoopRelay.Core.Prompts.RevisePlan.Template,
            global::LoopRelay.Core.Prompts.RevisePlan.SourceHash),
        Asset(
            "CollectDetails",
            "CollectDetails.prompt",
            global::LoopRelay.Core.Prompts.CollectDetails.Template,
            global::LoopRelay.Core.Prompts.CollectDetails.SourceHash),
        Asset(
            "ExtractDetails",
            "ExtractDetails.prompt",
            global::LoopRelay.Core.Prompts.ExtractDetails.Template,
            global::LoopRelay.Core.Prompts.ExtractDetails.SourceHash),
        Asset(
            "ExtractMilestones",
            "ExtractMilestones.prompt",
            global::LoopRelay.Core.Prompts.ExtractMilestones.Template,
            global::LoopRelay.Core.Prompts.ExtractMilestones.SourceHash),
        Asset(
            "GenerateHandoff",
            "GenerateHandoff.prompt",
            global::LoopRelay.Core.Prompts.GenerateHandoff.Template,
            global::LoopRelay.Core.Prompts.GenerateHandoff.SourceHash),
        Asset(
            "UpdateOperationalContext",
            "UpdateOperationalContext.prompt",
            global::LoopRelay.Core.Prompts.UpdateOperationalContext.Template,
            global::LoopRelay.Core.Prompts.UpdateOperationalContext.SourceHash),
    ];

    public static CanonicalPromptAsset GetByPromptIdentity(string promptIdentity) =>
        Assets.Single(asset => string.Equals(asset.PromptIdentity, promptIdentity, StringComparison.Ordinal));

    public static bool TryGetByPromptIdentity(
        string promptIdentity,
        out CanonicalPromptAsset asset)
    {
        CanonicalPromptAsset? match = Assets.FirstOrDefault(item =>
            string.Equals(item.PromptIdentity, promptIdentity, StringComparison.Ordinal));
        asset = match!;
        return match is not null;
    }

    public static bool HasAssetFor(WorkflowTransitionDefinition definition) =>
        TryGetByPromptIdentity(definition.PromptIdentity, out _);

    private static CanonicalPromptAsset Asset(
        string promptIdentity,
        string promptAssetName,
        string promptTemplate,
        string sourceHash) =>
        new(promptIdentity, promptAssetName, promptTemplate, sourceHash);
}
