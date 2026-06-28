using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandCenter.Backend.Tests;

/// <summary>
/// Contract Oracle goldens for the three orchestration SSE streams (m8 slice "Stream contracts"). Each SSE
/// frame is <c>id:&lt;seq&gt;\nevent:&lt;type&gt;\ndata:&lt;json&gt;\n\n</c>; the UI reconstructs the LOGICAL event
/// as <c>{ type, ...JSON.parse(data) }</c>. The contract golden for a stream is therefore a representative
/// ordered list of MERGED logical events — one representative per event type — serialized with the exact
/// backend JSON options and asserted byte-for-byte against its golden, refusing any additive drift. Field
/// names + types mirror the §8 TS run-event variants and the real producer's published anonymous objects.
/// </summary>
public sealed class OrchestrationStreamContractTests
{
    private static readonly JsonSerializerOptions BackendJsonOptions = CreateBackendJsonOptions();

    [Fact]
    public void PlanStreamGoldenFixtureMatchesBackendSerialization()
    {
        object trace = CreateRepresentativePlanStreamTrace();
        AssertMatchesGolden(trace, "plan-stream.golden.json", "Plan stream");
    }

    [Fact]
    public void ExecutionStreamGoldenFixtureMatchesBackendSerialization()
    {
        object trace = CreateRepresentativeExecutionStreamTrace();
        AssertMatchesGolden(trace, "execution-stream.golden.json", "Execution stream");
    }

    [Fact]
    public void DecisionStreamGoldenFixtureMatchesBackendSerialization()
    {
        object trace = CreateRepresentativeDecisionStreamTrace();
        AssertMatchesGolden(trace, "decision-stream.golden.json", "Decision stream");
    }

    // plan-stream — one representative per published plan event type. The producer publishes plan `failed`
    // as { reason, detail } (no phase), which matches the §8 PlanFailedEvent { reason, detail? } exactly.
    private static object CreateRepresentativePlanStreamTrace()
    {
        return new
        {
            stream = "plan",
            events = new object[]
            {
                new { type = "turn-started", phase = "WritePlan" },
                new { type = "delta", text = "Plan chunk." },
                new { type = "completed", plan = "RENDERED PLAN", promptTokens = 10, outputTokens = 20 },
                new { type = "failed", reason = "The planning agent run failed.", detail = "boom" },
            },
        };
    }

    // execution-stream — one representative per published execution event type. The producer publishes
    // execution `failed` with a phase (matching the §8 ExecutionRunFailedEvent { phase?, reason, detail? }).
    private static object CreateRepresentativeExecutionStreamTrace()
    {
        return new
        {
            stream = "execution",
            events = new object[]
            {
                new { type = "run-started", phase = "ExecutePlan" },
                new { type = "phase", phase = "ExtractMilestones" },
                new { type = "delta", phase = "StartExecution", text = "Execution chunk." },
                new { type = "milestones-extracted", count = 2 },
                new { type = "committed", commitSha = "commit-sha-0001", pushed = true },
                new { type = "lifecycle", state = "ExecutingPlan" },
                new { type = "handoff-rotated", sequence = 1, path = ".agents/handoffs/handoff.0001.md" },
                new
                {
                    type = "completed",
                    commitSha = "commit-sha-0001",
                    milestoneCount = 2,
                    handoffPath = ".agents/handoffs/handoff.0001.md",
                    promptTokens = 10,
                    outputTokens = 20,
                },
                new { type = "failed", phase = "StartExecution", reason = "The start execution run failed.", detail = "boom" },
            },
        };
    }

    // decision-stream — one representative per published decision event type. The producer publishes decision
    // `failed` with a phase (matching §8 DecisionRunFailedEvent { phase?, reason, detail? }).
    private static object CreateRepresentativeDecisionStreamTrace()
    {
        return new
        {
            stream = "decision",
            events = new object[]
            {
                new { type = "run-started", phase = "DecisionRun", route = "Continue" },
                new { type = "diagnostics", sandbox = "read-only", approvals = "never", seeded = true },
                new { type = "phase", phase = "GetNextDecisions" },
                new { type = "transferred", operationalDelta = ".agents/operational_delta.md", operationalContext = ".agents/operational_context.md" },
                new { type = "delta", text = "Decision chunk." },
                new { type = "completed", promptTokens = 10, outputTokens = 20 },
                new { type = "review-ready", decisions = "PROPOSED DECISIONS" },
                new { type = "submitted", path = ".agents/decisions/decisions.md", sequence = 1, numberedPath = ".agents/decisions/decisions.0001.md" },
                new { type = "failed", phase = "GetNextDecisions", reason = "The decision proposal run failed.", detail = "boom" },
            },
        };
    }

    private static void AssertMatchesGolden<T>(T value, string fileName, string contractName)
    {
        string actualJson = JsonSerializer.Serialize(value, BackendJsonOptions);
        string goldenPath = Path.Combine(AppContext.BaseDirectory, "ContractFixtures", fileName);

        string expectedJson = File.ReadAllText(goldenPath);

        using JsonDocument expected = JsonDocument.Parse(expectedJson);
        using JsonDocument actual = JsonDocument.Parse(actualJson);

        StreamContractAssert.MatchesFixture(
            expected.RootElement,
            actual.RootElement,
            StreamContractDriftPolicy.NoCompatibilityDriftAllowed(contractName));
    }

    private static JsonSerializerOptions CreateBackendJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
