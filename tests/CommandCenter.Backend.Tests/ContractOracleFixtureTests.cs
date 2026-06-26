using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;

namespace CommandCenter.Backend.Tests;

public sealed class ContractOracleFixtureTests
{
    private static readonly JsonSerializerOptions BackendJsonOptions = CreateBackendJsonOptions();

    [Fact]
    public void RepositoryDashboardGoldenFixtureMatchesBackendSerialization()
    {
        RepositoryDashboardProjection[] dashboard = [CreateRepresentativeRepositoryDashboardProjection()];
        string actualJson = JsonSerializer.Serialize(dashboard, BackendJsonOptions);
        string expectedJson = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ContractFixtures",
            "repository-dashboard.golden.json"));

        using JsonDocument expected = JsonDocument.Parse(expectedJson);
        using JsonDocument actual = JsonDocument.Parse(actualJson);

        JsonContractAssert.Equal(expected.RootElement, actual.RootElement, "$");
    }

    private static RepositoryDashboardProjection CreateRepresentativeRepositoryDashboardProjection()
    {
        DateTimeOffset startedAt = new(2026, 06, 24, 9, 57, 0, TimeSpan.Zero);
        DateTimeOffset completedAt = new(2026, 06, 24, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset generatedAt = new(2026, 06, 24, 10, 0, 0, TimeSpan.Zero);
        var sessionId = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var executionSummary = new ExecutionSessionSummary
        {
            SessionId = sessionId,
            State = ExecutionSessionState.Completed,
            RepositoryState = RepositoryExecutionState.AwaitingAcceptance,
            MilestonePath = ".agents/milestones/m4.md",
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Duration = TimeSpan.FromMinutes(3),
            AcceptedAt = null,
            RejectedAt = null,
            DecisionNote = null,
            LastActivityAt = completedAt,
            ProviderName = "codex",
            ProviderExecutablePath = null,
            ProviderProcessId = null,
            ProviderStartedAt = null,
            HandoffPath = ".agents/handoffs/handoff.md",
            CommitSha = null,
            CommittedAt = null,
            CommitMessage = null,
            PreparationSnapshotId = "prep-0001",
            PushAttemptedAt = null,
            PushedAt = null,
            PushedCommitSha = null,
            PushRemoteName = null,
            PushBranchName = null,
            FailureReason = null
        };

        return new RepositoryDashboardProjection
        {
            Repository = new Repository
            {
                Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Name = "FixtureRepository",
                Path = "C:/command-center-fixtures/FixtureRepository"
            },
            Availability = RepositoryAvailability.Available,
            Readiness = ExecutionReadiness.Ready,
            ExecutionState = RepositoryExecutionState.AwaitingAcceptance,
            ActiveExecutionSession = null,
            ExecutionSummary = executionSummary,
            ExecutionHistory = [executionSummary],
            MilestoneCount = 2,
            HasCurrentHandoff = true,
            HasCurrentDecisions = true,
            ContinuitySummary = new RepositoryContinuitySummary
            {
                OperationalContextExists = true,
                OperationalContextRevisionCount = 2,
                OperationalContextLastUpdatedAt = generatedAt.AddMinutes(-30),
                OpenQuestionCount = 1,
                ActiveRiskCount = 1,
                PendingProposalExists = false
            },
            ReasoningSummary = new RepositoryReasoningSummary
            {
                EventCount = 4,
                ThreadCount = 1,
                RelationshipCount = 1,
                HypothesisEventCount = 1,
                AlternativeEventCount = 1,
                ContradictionEventCount = 1,
                DirectionEventCount = 1,
                DecisionEvolutionEventCount = 0,
                AssumptionEvolutionEventCount = 0,
                ConstraintEvolutionEventCount = 0,
                EvidenceEventCount = 0,
                LastEventAt = generatedAt.AddMinutes(-20),
                LastThreadActivityAt = generatedAt.AddMinutes(-15),
                LastRelationshipAt = generatedAt.AddMinutes(-10),
                LastActivityAt = generatedAt.AddMinutes(-10),
                LastReconstructionAt = null,
                LastCertificationAt = null,
                CertificationResult = null
            },
            DecisionSessionSummary = new RepositoryDecisionSessionSummary
            {
                DecisionSessionId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                State = "Active",
                LifecycleDecision = "Transfer",
                TransferEligibilityStatus = "Eligible",
                EstimatedTokenCount = 250_000,
                EstimatedCacheTtl = TimeSpan.FromMinutes(12),
                CacheMissRisk = 0.42m,
                CoherenceScore = 0.67m,
                TransferPressure = 0.81m,
                HealthDimensions =
                [
                    new RepositoryDecisionSessionHealthDimension
                    {
                        Name = "Lifecycle",
                        Status = "Warning",
                        Findings = ["Transfer pressure is elevated."]
                    }
                ],
                RecentTransferLineage =
                [
                    new RepositoryDecisionSessionTransferSummary
                    {
                        TransferId = "transfer-1",
                        SourceSessionId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                        TargetSessionId = null,
                        ContinuityArtifactId = "artifact-1",
                        StartedAt = generatedAt.AddMinutes(-5),
                        CompletedAt = generatedAt.AddMinutes(-4),
                        Succeeded = true
                    }
                ],
                Diagnostics = ["registry warning"],
                GeneratedAt = generatedAt
            }
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

    private static class JsonContractAssert
    {
        public static void Equal(JsonElement expected, JsonElement actual, string path)
        {
            Assert.Equal(expected.ValueKind, actual.ValueKind);

            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    EqualObjects(expected, actual, path);
                    break;
                case JsonValueKind.Array:
                    EqualArrays(expected, actual, path);
                    break;
                case JsonValueKind.String:
                    Assert.Equal(expected.GetString(), actual.GetString());
                    break;
                case JsonValueKind.Number:
                    Assert.Equal(expected.GetRawText(), actual.GetRawText());
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    Assert.Equal(expected.GetBoolean(), actual.GetBoolean());
                    break;
                case JsonValueKind.Null:
                    Assert.Equal(JsonValueKind.Null, actual.ValueKind);
                    break;
                default:
                    Assert.Equal(expected.GetRawText(), actual.GetRawText());
                    break;
            }
        }

        private static void EqualObjects(JsonElement expected, JsonElement actual, string path)
        {
            Dictionary<string, JsonElement> expectedProperties = expected.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value);
            Dictionary<string, JsonElement> actualProperties = actual.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value);

            string[] missing = expectedProperties.Keys.Except(actualProperties.Keys, StringComparer.Ordinal).ToArray();
            string[] unexpected = actualProperties.Keys.Except(expectedProperties.Keys, StringComparer.Ordinal).ToArray();
            Assert.True(missing.Length == 0, $"{path} missing contract field(s): {string.Join(", ", missing)}");
            Assert.True(unexpected.Length == 0, $"{path} unexpected contract field(s): {string.Join(", ", unexpected)}");

            foreach ((string name, JsonElement expectedValue) in expectedProperties)
            {
                Equal(expectedValue, actualProperties[name], $"{path}.{name}");
            }
        }

        private static void EqualArrays(JsonElement expected, JsonElement actual, string path)
        {
            JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
            JsonElement[] actualItems = actual.EnumerateArray().ToArray();
            Assert.True(
                expectedItems.Length == actualItems.Length,
                $"{path} array length drift: expected {expectedItems.Length}, actual {actualItems.Length}");

            for (int i = 0; i < expectedItems.Length; i++)
            {
                Equal(expectedItems[i], actualItems[i], $"{path}[{i}]");
            }
        }
    }
}
