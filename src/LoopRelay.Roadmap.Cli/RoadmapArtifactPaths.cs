namespace LoopRelay.Roadmap.Cli;

internal static class RoadmapArtifactPaths
{
    public const string AgentsDirectory = ".agents";
    public const string State = ".agents/state.md";
    public const string StateJson = ".agents/state.json";
    public const string DecisionLedger = ".agents/decision-ledger.md";
    public const string DecisionLedgerJson = ".agents/decision-ledger.json";
    public const string Lifecycle = ".agents/artifacts/lifecycle.md";
    public const string LifecycleJson = ".agents/artifacts/lifecycle.json";
    public const string RoadmapFile = ".agents/roadmap.md";
    public const string RoadmapDirectory = ".agents/roadmap";
    public const string Selection = ".agents/selection.md";
    public const string SelectionProvenanceManifest = ".agents/selection-provenance-manifest.json";
    public const string ActiveEpic = ".agents/epic.md";
    public const string SpecsDirectory = ".agents/specs";
    public const string OperationalContext = ".agents/operational_context.md";
    public const string ExecutionPrompt = ".agents/execution-prompt.md";
    public const string ExecutionPreparationManifest = ".agents/execution-preparation-manifest.json";
    public const string ExecutionPlan = ".agents/plan.md";
    public const string ExecutionMilestonesDirectory = ".agents/milestones";
    public const string RoadmapCompletionContext = ".agents/core/roadmap-completion-context.md";
    public const string CompletedEpicsDirectory = ".agents/archive/epics";
    public const string CompletedEpicsPattern = ".agents/archive/epics/*.md";
    public const string ProjectionsManifest = ".agents/projections/manifest.md";
    public const string ProjectionsManifestJson = ".agents/projections/manifest.json";
    public const string PromptContracts = ".agents/contracts/prompt-contracts.md";
    public const string TransitionJournal = ".agents/journal/transitions.jsonl";
    public const string SplitFamiliesDirectory = ".agents/splits";
    public const string SelectionEvidenceDirectory = ".agents/evidence/selection";
    public const string AuditEvidenceDirectory = ".agents/evidence/audits";
    public const string ExecutionEvidenceDirectory = ".agents/evidence/execution";
    public const string EvaluationEvidenceDirectory = ".agents/evidence/evaluations";
    public const string BlockerEvidenceDirectory = ".agents/evidence/blockers";
    public const string OrchestrationEvidenceDirectory = ".agents/evidence/orchestration";
    public const string ProjectContextDirectory = ".agents/ctx";

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
            ["CreateRoadmapCompletionContext"] = ".agents/projections/roadmap-completion.md",
            ["UpdateRoadmapCompletionContext"] = ".agents/projections/roadmap-completion-update.md",
            ["SelectNextEpic"] = ".agents/projections/select-next-epic.md",
            ["EpicPreparationAudit"] = ".agents/projections/epic-preparation-audit.md",
            ["RealignEpic"] = ".agents/projections/realign-epic.md",
            ["ReimagineEpic"] = ".agents/projections/reimagine-epic.md",
            ["CreateNewEpic"] = ".agents/projections/create-new-epic.md",
            ["SplitEpic"] = ".agents/projections/split-epic.md",
            ["GenerateMilestoneDeepDivesForEpic"] = ".agents/projections/milestone-deep-dive.md",
            ["EvaluateEpicCompletionAndDrift"] = ".agents/projections/epic-completion-evaluation.md",
        };

    public static bool IsMilestoneSpecPath(string path) =>
        path.StartsWith($"{SpecsDirectory}/", StringComparison.OrdinalIgnoreCase) &&
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(Path.GetFileName(path), "bundle-manifest.md", StringComparison.OrdinalIgnoreCase);

    public static string SplitFamily(string familyId) => $".agents/splits/split-family-{familyId}.md";

    public static string SplitFamilyJson(string familyId) => $".agents/splits/split-family-{familyId}.json";
}
