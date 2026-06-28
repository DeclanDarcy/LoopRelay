using System.Text.Json;

namespace CommandCenter.Backend.Tests;

/// <summary>
/// Consumer-verification contracts for the orchestration SSE streams (m8 slice "Stream contracts"). The UI
/// reconstructs each logical event as <c>{ type, ...JSON.parse(data) }</c> and narrows it through a per-event
/// <c>export type</c> variant in <c>planning.ts</c> / <c>executionRun.ts</c> / <c>decisionRun.ts</c>. Each
/// [Fact] loads the representative merged event from the stream-trace golden, parses the matching TS variant
/// via <see cref="TypeScriptContractShapeProvider"/>, and asserts the variant accepts the backend payload with
/// zero drift — proving the compile-time consumer mirror stays faithful to the backend stream contract.
/// The union aliases (PlanStreamEvent / ExecutionRunEvent / DecisionRunEvent) are multi-arm and cannot be
/// GetShape-resolved (they hit Assert.Single(nonNullParts)); each arm is therefore verified individually.
/// </summary>
public sealed class OrchestrationConsumerContractTests
{
    [Fact]
    public void PlanStreamEventVariantsMatchTheStreamGolden()
    {
        VerifyEvent("plan-stream.golden.json", "turn-started", "PlanTurnStartedEvent");
        VerifyEvent("plan-stream.golden.json", "delta", "PlanDeltaEvent");
        VerifyEvent("plan-stream.golden.json", "completed", "PlanCompletedEvent");
        VerifyEvent("plan-stream.golden.json", "failed", "PlanFailedEvent");
    }

    [Fact]
    public void ExecutionStreamEventVariantsMatchTheStreamGolden()
    {
        VerifyEvent("execution-stream.golden.json", "run-started", "ExecutionRunStartedEvent");
        VerifyEvent("execution-stream.golden.json", "phase", "ExecutionRunPhaseEvent");
        VerifyEvent("execution-stream.golden.json", "delta", "ExecutionRunDeltaEvent");
        VerifyEvent("execution-stream.golden.json", "milestones-extracted", "ExecutionRunMilestonesExtractedEvent");
        VerifyEvent("execution-stream.golden.json", "committed", "ExecutionRunCommittedEvent");
        VerifyEvent("execution-stream.golden.json", "lifecycle", "ExecutionRunLifecycleEvent");
        VerifyEvent("execution-stream.golden.json", "handoff-rotated", "ExecutionRunHandoffRotatedEvent");
        VerifyEvent("execution-stream.golden.json", "completed", "ExecutionRunCompletedEvent");
        VerifyEvent("execution-stream.golden.json", "failed", "ExecutionRunFailedEvent");
    }

    [Fact]
    public void DecisionStreamEventVariantsMatchTheStreamGolden()
    {
        VerifyEvent("decision-stream.golden.json", "run-started", "DecisionRunStartedEvent");
        VerifyEvent("decision-stream.golden.json", "diagnostics", "DecisionRunDiagnosticsEvent");
        VerifyEvent("decision-stream.golden.json", "phase", "DecisionRunPhaseEvent");
        VerifyEvent("decision-stream.golden.json", "transferred", "DecisionRunTransferredEvent");
        VerifyEvent("decision-stream.golden.json", "delta", "DecisionRunDeltaEvent");
        VerifyEvent("decision-stream.golden.json", "completed", "DecisionRunCompletedEvent");
        VerifyEvent("decision-stream.golden.json", "review-ready", "DecisionRunReviewReadyEvent");
        VerifyEvent("decision-stream.golden.json", "submitted", "DecisionRunSubmittedEvent");
        VerifyEvent("decision-stream.golden.json", "failed", "DecisionRunFailedEvent");
    }

    [Fact]
    public void PlanStatusTypeScriptTypeMatchesGoldenFixture()
    {
        JsonElement planStatus = ReadGoldenRoot("plan-status.golden.json");
        TypeScriptContractShapeProvider shapes = ReadTypeScriptContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            "TypeScript PlanStatus",
            "compile-time consumer",
            shapes.GetShape("PlanStatus")));

        ConsumerContractDrift[] drifts = verifier.Compare("$", planStatus).ToArray();

        Assert.Empty(drifts);
    }

    private static void VerifyEvent(string goldenFileName, string eventType, string variantTypeName)
    {
        JsonElement eventElement = FindEvent(goldenFileName, eventType);
        TypeScriptContractShapeProvider shapes = ReadTypeScriptContractShapes();
        ContractConsumerVerifier verifier = new(new ConsumerContractVerifierSpec(
            $"TypeScript {variantTypeName}",
            "compile-time consumer",
            shapes.GetShape(variantTypeName)));

        ConsumerContractDrift[] drifts = verifier.Compare($"$.events[{eventType}]", eventElement).ToArray();

        Assert.Empty(drifts);
    }

    private static JsonElement FindEvent(string goldenFileName, string eventType)
    {
        JsonElement root = ReadGoldenRoot(goldenFileName);
        foreach (JsonElement candidate in root.GetProperty("events").EnumerateArray())
        {
            if (candidate.GetProperty("type").GetString() == eventType)
            {
                return candidate.Clone();
            }
        }

        throw new InvalidOperationException($"Event '{eventType}' not found in {goldenFileName}.");
    }

    private static JsonElement ReadGoldenRoot(string goldenFileName)
    {
        string json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "ContractFixtures", goldenFileName));
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private static TypeScriptContractShapeProvider ReadTypeScriptContractShapes()
    {
        DirectoryInfo repositoryRoot = FindRepositoryRoot();
        DirectoryInfo typesDirectory = new(Path.Combine(
            repositoryRoot.FullName, "src", "CommandCenter.UI", "src", "types"));
        DirectoryInfo generatedContractsDirectory = new(Path.Combine(
            repositoryRoot.FullName, "src", "CommandCenter.UI", "src", "contracts", "generated"));
        IEnumerable<DirectoryInfo> directories = generatedContractsDirectory.Exists
            ? [typesDirectory, generatedContractsDirectory]
            : [typesDirectory];
        return TypeScriptContractShapeProvider.Parse(directories);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "CommandCenter.Shell", "src", "main.rs")))
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }
}
