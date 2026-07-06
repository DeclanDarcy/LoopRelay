namespace LoopRelay.Roadmap.Cli;

internal sealed class PromptContractRegistry
{
    private readonly IReadOnlyDictionary<string, PromptContract> contracts;

    public PromptContractRegistry(ProjectionRegistry projectionRegistry)
    {
        contracts = new[]
        {
            Contract("CreateRoadmapCompletionContext", [], [RoadmapArtifactPaths.RoadmapCompletionContext], [], "RoadmapCompletionContextWriter", StaleProjectionPolicy.Block, "None"),
            Contract("UpdateRoadmapCompletionContext", [RoadmapArtifactPaths.RoadmapCompletionContext, RoadmapArtifactPaths.ActiveEpic], [RoadmapArtifactPaths.RoadmapCompletionContext], ["Close Epic", "Close With Follow-Up"], "RoadmapCompletionContextWriter", StaleProjectionPolicy.Block, "None"),
            Contract("SelectNextEpic", [RoadmapArtifactPaths.RoadmapCompletionContext, RoadmapArtifactPaths.RoadmapFile], [RoadmapArtifactPaths.Selection], ["Select Existing Epic", "Select New Intermediary Epic", "Select Split Epic", "Strategic Investigation Required", "Roadmap Revision Required", "No Suitable Initiative"], "SelectionWriter", StaleProjectionPolicy.Block, "SelectionParser"),
            Contract("EpicPreparationAudit", [RoadmapArtifactPaths.Selection], [RoadmapArtifactPaths.AuditEvidenceDirectory], ["Realign", "Reimagine", "Retire", "Insufficient Evidence"], "AuditEvidenceWriter", StaleProjectionPolicy.Block, "EpicPreparationAuditParser"),
            Contract("RealignEpic", [RoadmapArtifactPaths.ActiveEpic], [RoadmapArtifactPaths.ActiveEpic], ["Realign"], "ArtifactPromotionService", StaleProjectionPolicy.Block, "EpicAuthoringOutputClassifier+EpicArtifactValidator", ["# Epic Realignment Blocked"]),
            Contract("ReimagineEpic", [RoadmapArtifactPaths.ActiveEpic], [RoadmapArtifactPaths.ActiveEpic], ["Reimagine"], "ArtifactPromotionService", StaleProjectionPolicy.Block, "EpicAuthoringOutputClassifier+EpicArtifactValidator", ["# Epic Reimagination Blocked"]),
            Contract("CreateNewEpic", [RoadmapArtifactPaths.Selection], [RoadmapArtifactPaths.ActiveEpic], ["Create Epic"], "ArtifactPromotionService", StaleProjectionPolicy.Block, "EpicAuthoringOutputClassifier+EpicArtifactValidator", ["# Create New Epic Blocked"]),
            Contract("SplitEpic", [RoadmapArtifactPaths.Selection], [RoadmapArtifactPaths.SplitFamiliesDirectory, RoadmapArtifactPaths.ActiveEpic], ["Split Epic"], "SplitEpicBundleInterpreter+SplitFamilyStore+ArtifactPromotionService", StaleProjectionPolicy.Block, "BundleFileExtractor+SplitEpicBundleInterpreter+EpicArtifactValidator", ["# Split Epic Blocked"]),
            Contract("GenerateMilestoneDeepDivesForEpic", [RoadmapArtifactPaths.ActiveEpic], [RoadmapArtifactPaths.SpecsDirectory], ["Generate Specs"], "SpecBundleWriter", StaleProjectionPolicy.Block, "BundleFileExtractor"),
            Contract("EvaluateEpicCompletionAndDrift", [RoadmapArtifactPaths.ActiveEpic, RoadmapArtifactPaths.SpecsDirectory], [RoadmapArtifactPaths.EvaluationEvidenceDirectory], CompletionCertificationRouter.AllowedRecommendations, "EvaluationEvidenceWriter", StaleProjectionPolicy.Block, "CompletionEvaluationParser"),
        }.ToDictionary(contract => contract.RuntimePromptName, StringComparer.Ordinal);

        string[] missing = projectionRegistry.All
            .Select(definition => definition.RuntimePromptName)
            .Where(runtimePromptName => !contracts.ContainsKey(runtimePromptName))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException("Prompt contract registry is missing contracts for: " + string.Join(", ", missing));
        }
    }

    public IReadOnlyCollection<PromptContract> All => contracts.Values.ToList();

    public PromptContract Get(string runtimePromptName) =>
        contracts.TryGetValue(runtimePromptName, out PromptContract? contract)
            ? contract
            : throw new ArgumentOutOfRangeException(nameof(runtimePromptName), runtimePromptName, "No prompt contract registered.");

    public bool Contains(string runtimePromptName) => contracts.ContainsKey(runtimePromptName);

    public async Task EmitSnapshotAsync(RoadmapArtifacts artifacts)
    {
        var lines = new List<string>
        {
            "# Prompt Contracts",
            string.Empty,
            "| Runtime Prompt | Projection | Required Inputs | Required Outputs | Allowed Decisions | Blocking Outputs | Artifact Writer | Stale Projection Policy | Parser |",
            "|---|---|---|---|---|---|---|---|---|",
        };

        foreach (PromptContract contract in All.OrderBy(contract => contract.RuntimePromptName, StringComparer.Ordinal))
        {
            lines.Add(
                $"| {contract.RuntimePromptName} | {contract.RequiredProjectionRuntimePrompt} | {Join(contract.RequiredInputs)} | {Join(contract.RequiredOutputs)} | {Join(contract.AllowedDecisions)} | {Join(contract.BlockingOutputs)} | {contract.ArtifactWriter} | {contract.StaleProjectionPolicy} | {contract.ParserName} |");
        }

        await artifacts.WriteAsync(RoadmapArtifactPaths.PromptContracts, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static PromptContract Contract(
        string runtimePromptName,
        IReadOnlyList<string> requiredInputs,
        IReadOnlyList<string> requiredOutputs,
        IReadOnlyList<string> allowedDecisions,
        string writer,
        StaleProjectionPolicy stalePolicy,
        string parserName,
        IReadOnlyList<string>? blockingOutputs = null) =>
        new(
            runtimePromptName,
            runtimePromptName,
            requiredInputs,
            [],
            requiredOutputs,
            allowedDecisions,
            blockingOutputs ?? [],
            writer,
            stalePolicy,
            parserName);

    private static string Join(IReadOnlyList<string> values) => values.Count == 0 ? "None" : string.Join("<br>", values);
}

internal sealed record PromptContract(
    string RuntimePromptName,
    string RequiredProjectionRuntimePrompt,
    IReadOnlyList<string> RequiredInputs,
    IReadOnlyList<string> OptionalInputs,
    IReadOnlyList<string> RequiredOutputs,
    IReadOnlyList<string> AllowedDecisions,
    IReadOnlyList<string> BlockingOutputs,
    string ArtifactWriter,
    StaleProjectionPolicy StaleProjectionPolicy,
    string ParserName);

internal enum StaleProjectionPolicy
{
    Block,
    WarnOnly,
    Allow,
}
