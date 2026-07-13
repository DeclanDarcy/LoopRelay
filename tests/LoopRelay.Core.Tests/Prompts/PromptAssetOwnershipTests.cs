namespace LoopRelay.Core.Tests.Prompts;

/// <summary>
/// The prompt-asset ownership registry (M6 Prompt Authority: zero unowned prompt assets).
/// Every file under src/LoopRelay.Core/Prompts must appear here with its registered owner, and
/// every registry entry must exist on disk — a new asset without a declared owner, or a deleted
/// asset with a stale registration, fails this suite. Owners name the production consumer
/// (catalog or direct generated-class call site); "owner-reserved" marks assets the owner
/// explicitly retained for near-future plans (2026-07-11 ruling) that no production path
/// consumes yet.
/// </summary>
public sealed class PromptAssetOwnershipTests
{
    // Relative path (forward slashes) -> registered owner.
    private static readonly IReadOnlyDictionary<string, string> Registry =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Canonical prompt asset catalog (rendered at the TransitionRuntime seam).
            ["AdversarialPlanReview.prompt"] = "CanonicalPromptAssetCatalog:RunAdversarialReview",
            ["CollectDetails.prompt"] = "CanonicalPromptAssetCatalog:CollectDetails",
            ["ExtractDetails.prompt"] = "CanonicalPromptAssetCatalog:ExtractDetails",
            ["ExtractMilestones.prompt"] = "CanonicalPromptAssetCatalog:ExtractMilestones",
            ["GenerateHandoff.prompt"] = "CanonicalPromptAssetCatalog:GenerateHandoff + UnifiedPromptExecutor handoff turn",
            ["RevisePlan.prompt"] = "CanonicalPromptAssetCatalog:ReviewAndRevisePlan + Plan.Cli PlanSession",
            ["UpdateOperationalContext.prompt"] = "CanonicalPromptAssetCatalog:UpdateOperationalContext + DecisionSession transfer",
            ["WritePlan.prompt"] = "CanonicalPromptAssetCatalog:WritePlan + Plan.Cli PlanSession",
            ["Planning/CreateNewEpic.prompt"] = "CanonicalPromptAssetCatalog:CreateNewEpic",
            ["Planning/CreateRoadmapCompletionContext.prompt"] = "CanonicalPromptAssetCatalog:BootstrapRoadmapCompletionContext + CompletionPromptCatalog",
            ["Planning/EpicPreparationAudit.prompt"] = "CanonicalPromptAssetCatalog:AuditExistingEpic",
            ["Planning/GenerateMilestoneDeepDivesForEpic.prompt"] = "CanonicalPromptAssetCatalog:GenerateMilestoneDeepDivesForEpic",
            ["Planning/RealignEpic.prompt"] = "CanonicalPromptAssetCatalog:RealignEpic",
            ["Planning/ReimagineEpic.prompt"] = "CanonicalPromptAssetCatalog:ReimagineEpic",
            ["Planning/SelectNextEpic.prompt"] = "CanonicalPromptAssetCatalog:SelectStrategicInitiative",
            ["Planning/SplitEpic.prompt"] = "CanonicalPromptAssetCatalog:SplitEpic",
            ["Planning/UpdateRoadmapCompletionContext.prompt"] = "CanonicalPromptAssetCatalog:UpdateRoadmapCompletionContext + CompletionPromptCatalog",

            // Eval prompt asset catalog.
            ["Eval/CreateArchitecturalCatalog.prompt"] = "EvalPromptAssetCatalog",
            ["Eval/CreateEvalDag.prompt"] = "EvalPromptAssetCatalog",
            ["Eval/CreateEvalDependencyInventory.prompt"] = "EvalPromptAssetCatalog",
            ["Eval/CreateEvalHypothesisInventory.prompt"] = "EvalPromptAssetCatalog",
            ["Eval/CreateNextEpicImplementationSpec.prompt"] = "EvalPromptAssetCatalog",
            ["Eval/CreateNextEpicRoadmap.prompt"] = "EvalPromptAssetCatalog",
            ["Eval/UpdateDependencyInventory.prompt"] = "EvalPromptAssetCatalog (owner-reserved: content pending)",
            ["Eval/UpdateHypothesisInventory.prompt"] = "EvalPromptAssetCatalog (owner-reserved: content pending)",
            ["Eval/UpdateRoadmap.prompt"] = "EvalPromptAssetCatalog (owner-reserved: content pending)",

            // Completion prompt catalog (agent completion prompt runner).
            ["Planning/EvaluateEpicCompletionAndDrift.prompt"] = "CompletionPromptCatalog",
            ["Planning/SynthesizeCompletedEpic.prompt"] = "CompletionPromptCatalog",

            // Projection prompt catalog (projection prompt runner).
            ["Projections/ProjectionForAdversarialPlanReview.prompt"] = "CanonicalPromptAssetCatalog:GenerateAdversarialProjection + ProjectionPromptCatalog",
            ["Projections/ProjectionForCreateNewEpic.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForCreateRoadmapCompletionContext.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForDecisionSession.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForEpicPreparationAudit.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForEvaluateEpicCompletionAndDrift.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForGenerateMilestoneDeepDivesForEpic.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForRealignEpic.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForReimagineEpic.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForSelectNextEpic.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForSplitEpic.prompt"] = "ProjectionPromptCatalog",
            ["Projections/ProjectionForUpdateRoadmapCompletionContext.prompt"] = "ProjectionPromptCatalog",

            // Direct generated-class consumers.
            ["GenerateNoChangesHandoff.prompt"] = "UnifiedPromptExecutor handoff turn + legacy ExecutionStep",
            ["GenerateSystemPromptForFirstExecutionAgent.prompt"] = "DecisionSession proposal turn",
            ["GenerateSystemPromptForNextExecutionAgent.prompt"] = "DecisionSession proposal turn",
            ["OptimizeOperationalDocuments.prompt"] = "DecisionSession transfer",
            ["ProduceOperationalDelta.prompt"] = "DecisionSession transfer",
            ["RecoverDecisionSessionContext.prompt"] = "DecisionSession recovery",
            ["ConfirmNonImplementationCandidate.prompt"] = "NonImplementationSemanticConfirmer",
            ["SynthesizeNonImplementationInsights.prompt"] = "NonImplementationInsightSynthesizer",

            // Legacy Execute loop seed (LoopRunner/ExecutionStep), converging M17-M19.

        };

    // Registered assets whose template content the owner has explicitly reserved as pending;
    // every other .prompt template must be non-empty.
    private static readonly IReadOnlySet<string> ContentPendingAssets =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "Eval/UpdateDependencyInventory.prompt",
            "Eval/UpdateHypothesisInventory.prompt",
            "Eval/UpdateRoadmap.prompt",
        };

    [Fact]
    public void Every_prompt_asset_on_disk_has_exactly_one_registered_owner()
    {
        string promptsRoot = PromptsRoot();
        string[] onDisk = Directory.EnumerateFiles(promptsRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(promptsRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        string[] registered = Registry.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray();

        Assert.Equal(registered, onDisk);
    }

    [Fact]
    public void Every_registered_template_has_content_unless_explicitly_reserved_as_pending()
    {
        string promptsRoot = PromptsRoot();
        foreach (string asset in Registry.Keys.Where(key => key.EndsWith(".prompt", StringComparison.Ordinal)))
        {
            string content = File.ReadAllText(Path.Combine(promptsRoot, asset));
            if (ContentPendingAssets.Contains(asset))
            {
                continue;
            }

            Assert.False(
                string.IsNullOrWhiteSpace(content),
                $"Registered prompt asset `{asset}` has an empty template but is not owner-reserved as content pending.");
        }
    }

    private static string PromptsRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "src", "LoopRelay.Core", "Prompts");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate src/LoopRelay.Core/Prompts.");
    }
}
