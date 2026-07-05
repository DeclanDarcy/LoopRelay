namespace CommandCenter.Roadmap.Cli;

internal static class RoadmapArtifactPaths
{
    public const string AgentsDirectory = ".agents";
    public const string State = ".agents/state.md";
    public const string DecisionLedger = ".agents/decision-ledger.md";
    public const string Lifecycle = ".agents/artifacts/lifecycle.md";
    public const string RoadmapFile = ".agents/roadmap.md";
    public const string RoadmapDirectory = ".agents/roadmap";
    public const string Selection = ".agents/selection.md";
    public const string ActiveEpic = ".agents/epic.md";
    public const string SpecsDirectory = ".agents/specs";
    public const string OperationalContext = ".agents/operational_context.md";
    public const string ExecutionPrompt = ".agents/execution-prompt.md";
    public const string RoadmapCompletionContext = ".agents/north-star/roadmap-completion-context.md";
    public const string ProjectionsManifest = ".agents/projections/manifest.md";
    public const string PromptContracts = ".agents/contracts/prompt-contracts.md";
    public const string TransitionJournal = ".agents/journal/transitions.jsonl";
    public const string SplitFamiliesDirectory = ".agents/splits";
    public const string SelectionEvidenceDirectory = ".agents/evidence/selection";
    public const string AuditEvidenceDirectory = ".agents/evidence/audits";
    public const string EvaluationEvidenceDirectory = ".agents/evidence/evaluations";
    public const string BlockerEvidenceDirectory = ".agents/evidence/blockers";
    public const string OrchestrationEvidenceDirectory = ".agents/evidence/orchestration";

    public static readonly IReadOnlyList<string> NorthStarSourceFiles =
    [
        ".agents/north-star/01-purpose.md",
        ".agents/north-star/02-capability-model.md",
        ".agents/north-star/03-invariants.md",
        ".agents/north-star/04-strategic-structure.md",
        ".agents/north-star/05-authority-model.md",
        ".agents/north-star/06-evaluation-model.md",
        ".agents/north-star/07-drift-and-false-success.md",
        ".agents/north-star/08-vocabulary.md",
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

    public static string SplitFamily(string familyId) => $".agents/splits/split-family-{familyId}.md";
}
