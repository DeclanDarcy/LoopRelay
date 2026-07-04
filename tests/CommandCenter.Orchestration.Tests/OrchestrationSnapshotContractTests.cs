using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Core.Prompts;
using CommandCenter.Orchestration.Models;

namespace CommandCenter.Orchestration.Tests;

/// <summary>
/// Contract Oracle goldens for the orchestration snapshot + command-acknowledgement contracts (m8 slice
/// "Snapshot + command contracts"). The authority for each contract is the backend-serialized JSON captured
/// as a <c>ContractFixtures/&lt;id&gt;.golden.json</c> fixture; each [Fact] re-serializes a deterministic
/// representative with the exact backend JSON options and asserts byte-for-byte structural equality against
/// its golden, refusing any additive drift. The csproj globs <c>ContractFixtures/*.json</c> to the output dir,
/// so the goldens resolve at runtime via <see cref="AppContext.BaseDirectory"/>.
/// </summary>
public sealed class OrchestrationSnapshotContractTests
{
    private static readonly JsonSerializerOptions BackendJsonOptions = CreateBackendJsonOptions();

    [Fact]
    public void PlanStatusGoldenFixtureMatchesBackendSerialization()
    {
        PlanStatus value = CreateRepresentativePlanStatus();
        AssertMatchesGolden(value, "plan-status.golden.json", "Plan status");
    }

    [Fact]
    public void PlanStatusAuthoringGoldenFixtureMatchesBackendSerialization()
    {
        PlanStatus value = CreateRepresentativePlanStatusAuthoring();
        AssertMatchesGolden(value, "plan-status-authoring.golden.json", "Plan status (authoring)");
    }

    [Fact]
    public void PlanRunAcknowledgementGoldenFixtureMatchesBackendSerialization()
    {
        PlanRunAcknowledgement value = CreateRepresentativePlanRunAcknowledgement();
        AssertMatchesGolden(value, "plan-run-acknowledgement.golden.json", "Plan run acknowledgement");
    }

    [Fact]
    public void OrchestrationErrorGoldenFixtureMatchesBackendSerialization()
    {
        object value = CreateRepresentativeOrchestrationError();
        AssertMatchesGolden(value, "orchestration-error.golden.json", "Orchestration error");
    }

    [Fact]
    public void ConversationGoldenFixtureMatchesBackendSerialization()
    {
        ConversationProjection value = CreateRepresentativeConversationProjection();
        AssertMatchesGolden(value, "conversation.golden.json", "Conversation");
    }

    [Fact]
    public void PromptProvenanceGoldenFixtureMatchesBackendSerialization()
    {
        PromptProvenance value = CreateRepresentativePromptProvenance();
        AssertMatchesGolden(value, "prompt-provenance.golden.json", "Prompt provenance");
    }

    private static void AssertMatchesGolden<T>(T value, string fileName, string contractName)
    {
        string actualJson = JsonSerializer.Serialize(value, BackendJsonOptions);
        string expectedJson = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            fileName));

        using JsonDocument expected = JsonDocument.Parse(expectedJson);
        using JsonDocument actual = JsonDocument.Parse(actualJson);

        StreamContractAssert.MatchesFixture(
            expected.RootElement,
            actual.RootElement,
            StreamContractDriftPolicy.NoCompatibilityDriftAllowed(contractName));
    }

    // plan-status — repository-lifecycle-state contract. State serializes as the string "ExecutingPlan".
    private static PlanStatus CreateRepresentativePlanStatus()
    {
        return new PlanStatus(PlanExists: true, State: PlanLifecycleState.ExecutingPlan);
    }

    // plan-status (authoring) — the other PlanLifecycleState wire string. State serializes as "PlanAuthoring".
    private static PlanStatus CreateRepresentativePlanStatusAuthoring()
    {
        return new PlanStatus(PlanExists: false, State: PlanLifecycleState.PlanAuthoring);
    }

    // plan-run-acknowledgement — Phase vocabulary: WritePlan|RevisePlan|ExecutePlan|DecisionRun|SubmitDecisions.
    private static PlanRunAcknowledgement CreateRepresentativePlanRunAcknowledgement()
    {
        return new PlanRunAcknowledgement("WritePlan");
    }

    // orchestration-error — the structured { error } shape every endpoint returns on a faulted command.
    private static object CreateRepresentativeOrchestrationError()
    {
        return new { error = "Repository 11111111-1111-1111-1111-111111111111 was not found." };
    }

    // conversation — entries spanning Planning / OperationalOutput / Submit, with one null Reference pinned.
    private static ConversationProjection CreateRepresentativeConversationProjection()
    {
        return new ConversationProjection(new[]
        {
            new ConversationEntry(
                Sequence: 1,
                Kind: ConversationEntryKind.Planning,
                Iteration: 0,
                Summary: "Authored the plan.",
                Reference: ".agents/plan.md"),
            new ConversationEntry(
                Sequence: 2,
                Kind: ConversationEntryKind.OperationalOutput,
                Iteration: 1,
                Summary: "Rotated the first handoff.",
                Reference: ".agents/handoffs/handoff.0001.md"),
            new ConversationEntry(
                Sequence: 3,
                Kind: ConversationEntryKind.Submit,
                Iteration: 1,
                Summary: "Submitted the edited decisions.",
                Reference: null),
        });
    }

    // prompt-provenance — a fully-populated provenance record bound to a REAL prompt identity from the catalog
    // (ContinueExecution). The WorkflowPhase + input/output artifact identities mirror what
    // RepositoryOrchestrator.BuildContinueExecutionProvenance records. SessionRole serializes as a string.
    private static PromptProvenance CreateRepresentativePromptProvenance()
    {
        return new PromptProvenance
        {
            PromptName = nameof(ContinueExecution),
            PromptType = typeof(ContinueExecution).FullName!,
            SourceHash = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF",
            SessionRole = PromptSessionRole.OperationalExecution,
            WorkflowPhase = "ContinueExecution",
            InputArtifactIdentities = new[]
            {
                ".agents/plan.md",
                ".agents/handoffs/handoff.0001.md",
                ".agents/decisions/decisions.md",
            },
            OutputArtifactIdentities = new[]
            {
                ".agents/handoffs/handoff.md",
            },
        };
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
