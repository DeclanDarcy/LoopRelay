using LoopRelay.Orchestration.Services;

namespace LoopRelay.Roadmap.Cli.Services;

internal static class RoadmapArtifactPaths
{
    public const string AgentsDirectory = OrchestrationArtifactPaths.AgentsDirectory;
    public const string EvidenceDirectory = OrchestrationArtifactPaths.EvidenceDirectory;
    public const string State = AgentsDirectory + "/state.md";
    public const string StateJson = AgentsDirectory + "/state.json";
    public const string DecisionLedger = AgentsDirectory + "/decision-ledger.md";
    public const string DecisionLedgerJson = AgentsDirectory + "/decision-ledger.json";
    public const string ArtifactsDirectory = AgentsDirectory + "/artifacts";
    public const string Lifecycle = ArtifactsDirectory + "/lifecycle.md";
    public const string LifecycleJson = ArtifactsDirectory + "/lifecycle.json";
    public const string RoadmapDirectory = AgentsDirectory + "/roadmap";
    public const string RoadmapDirectoryPattern = RoadmapDirectory + "/*.md";
    public const string Selection = AgentsDirectory + "/selection.md";
    public const string SelectionProvenanceManifest = AgentsDirectory + "/selection-provenance-manifest.json";
    public const string ActiveEpic = AgentsDirectory + "/epic.md";
    public const string SpecsDirectory = OrchestrationArtifactPaths.SpecsDirectory;
    public const string OperationalContext = OrchestrationArtifactPaths.OperationalContext;
    public const string ExecutionPrompt = AgentsDirectory + "/execution-prompt.md";
    public const string ExecutionPreparationManifest = AgentsDirectory + "/execution-preparation-manifest.json";
    public const string ExecutionPlan = OrchestrationArtifactPaths.Plan;
    public const string ExecutionMilestonesDirectory = OrchestrationArtifactPaths.MilestonesDirectory;
    public const string CoreDirectory = AgentsDirectory + "/core";
    public const string RoadmapCompletionContext = CoreDirectory + "/roadmap-completion-context.md";
    public const string ArchiveDirectory = AgentsDirectory + "/archive";
    public const string CompletedEpicsDirectory = ArchiveDirectory + "/epics";
    public const string CompletedEpicsPattern = CompletedEpicsDirectory + "/*.md";
    public const string ProjectionsDirectory = AgentsDirectory + "/projections";
    public const string ProjectionsManifest = ProjectionsDirectory + "/manifest.md";
    public const string ProjectionsManifestJson = ProjectionsDirectory + "/manifest.json";
    public const string PromptContracts = AgentsDirectory + "/contracts/prompt-contracts.md";
    public const string TransitionJournal = AgentsDirectory + "/journal/transitions.jsonl";
    public const string SplitFamiliesDirectory = AgentsDirectory + "/splits";
    public const string SelectionEvidenceDirectory = EvidenceDirectory + "/selection";
    public const string AuditEvidenceDirectory = EvidenceDirectory + "/audits";
    public const string ExecutionEvidenceDirectory = EvidenceDirectory + "/execution";
    public const string EvaluationEvidenceDirectory = EvidenceDirectory + "/evaluations";
    public const string BlockerEvidenceDirectory = EvidenceDirectory + "/blockers";
    public const string OrchestrationEvidenceDirectory = EvidenceDirectory + "/orchestration";
    public const string ProjectContextDirectory = AgentsDirectory + "/ctx";

    public static readonly IReadOnlyList<string> ProjectContextSourceFiles =
    [
        $"{ProjectContextDirectory}/01-purpose.md",
        $"{ProjectContextDirectory}/02-capability-model.md",
        $"{ProjectContextDirectory}/03-invariants.md",
        $"{ProjectContextDirectory}/04-strategic-structure.md",
        $"{ProjectContextDirectory}/05-authority-model.md",
        $"{ProjectContextDirectory}/06-evaluation-model.md",
        $"{ProjectContextDirectory}/07-drift-and-false-success.md",
        $"{ProjectContextDirectory}/08-vocabulary.md",
    ];

    public static readonly IReadOnlyDictionary<string, string> ProjectionPaths =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CreateRoadmapCompletionContext"] = $"{ProjectionsDirectory}/roadmap-completion.md",
            ["UpdateRoadmapCompletionContext"] = $"{ProjectionsDirectory}/roadmap-completion-update.md",
            ["SelectNextEpic"] = $"{ProjectionsDirectory}/select-next-epic.md",
            ["EpicPreparationAudit"] = $"{ProjectionsDirectory}/epic-preparation-audit.md",
            ["RealignEpic"] = $"{ProjectionsDirectory}/realign-epic.md",
            ["ReimagineEpic"] = $"{ProjectionsDirectory}/reimagine-epic.md",
            ["CreateNewEpic"] = $"{ProjectionsDirectory}/create-new-epic.md",
            ["SplitEpic"] = $"{ProjectionsDirectory}/split-epic.md",
            ["GenerateMilestoneDeepDivesForEpic"] = $"{ProjectionsDirectory}/milestone-deep-dive.md",
            ["EvaluateEpicCompletionAndDrift"] = $"{ProjectionsDirectory}/epic-completion-evaluation.md",
        };

    public static bool IsMilestoneSpecPath(string path) =>
        path.StartsWith($"{SpecsDirectory}/", StringComparison.OrdinalIgnoreCase) &&
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(Path.GetFileName(path), "bundle-manifest.md", StringComparison.OrdinalIgnoreCase);

    public static string SplitFamily(string familyId) => $"{SplitFamiliesDirectory}/split-family-{familyId}.md";

    public static string SplitFamilyJson(string familyId) => $"{SplitFamiliesDirectory}/split-family-{familyId}.json";
}
