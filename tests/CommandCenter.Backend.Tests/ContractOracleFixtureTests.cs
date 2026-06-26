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

        JsonContractAssert.MatchesFixture(
            expected.RootElement,
            actual.RootElement,
            ContractDriftPolicy.NoCompatibilityDriftAllowed("Repository dashboard"));
    }

    [Fact]
    public void ContractOracleClassifiesMissingFieldAsStructuralDrift()
    {
        using JsonDocument expected = JsonDocument.Parse("""{"name":"Repository","summary":{"count":1}}""");
        using JsonDocument actual = JsonDocument.Parse("""{"summary":{"count":1}}""");

        ContractDrift[] drift = JsonContractAssert.Compare(expected.RootElement, actual.RootElement).ToArray();

        ContractDrift missingField = Assert.Single(drift);
        Assert.Equal(ContractDriftCategory.Structural, missingField.Category);
        Assert.Equal(ContractDriftKind.MissingField, missingField.Kind);
        Assert.Equal("$.name", missingField.Path);
    }

    [Fact]
    public void ContractOracleClassifiesAdditiveFieldAsCompatibilityReviewDrift()
    {
        using JsonDocument expected = JsonDocument.Parse("""{"name":"Repository"}""");
        using JsonDocument actual = JsonDocument.Parse("""{"name":"Repository","displayName":"Repository"}""");

        ContractOracleDriftException exception = Assert.Throws<ContractOracleDriftException>(() =>
            JsonContractAssert.MatchesFixture(
                expected.RootElement,
                actual.RootElement,
                ContractDriftPolicy.NoCompatibilityDriftAllowed("Repository dashboard")));

        Assert.Contains("compatibility review drift", exception.Message, StringComparison.Ordinal);
        Assert.Contains("$.displayName", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContractOracleAllowsReviewedCompatibilityAdditiveField()
    {
        using JsonDocument expected = JsonDocument.Parse("""{"name":"Repository"}""");
        using JsonDocument actual = JsonDocument.Parse("""{"name":"Repository","displayName":"Repository"}""");

        JsonContractAssert.MatchesFixture(
            expected.RootElement,
            actual.RootElement,
            ContractDriftPolicy.WithReviewedCompatibilityAdditions(
                "Repository dashboard",
                "$.displayName"));
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
        public static void MatchesFixture(JsonElement expected, JsonElement actual, ContractDriftPolicy policy)
        {
            ContractDrift[] drifts = Compare(expected, actual, policy).ToArray();
            if (drifts.Length == 0)
            {
                return;
            }

            throw new ContractOracleDriftException(policy.ContractName, drifts);
        }

        public static IEnumerable<ContractDrift> Compare(JsonElement expected, JsonElement actual)
        {
            return Compare(expected, actual, ContractDriftPolicy.NoCompatibilityDriftAllowed("Contract"));
        }

        private static IEnumerable<ContractDrift> Compare(JsonElement expected, JsonElement actual, ContractDriftPolicy policy)
        {
            return Compare(expected, actual, "$", policy);
        }

        private static IEnumerable<ContractDrift> Compare(
            JsonElement expected,
            JsonElement actual,
            string path,
            ContractDriftPolicy policy)
        {
            if (expected.ValueKind != actual.ValueKind)
            {
                yield return ContractDrift.Structural(
                    ContractDriftKind.ValueKindChanged,
                    path,
                    $"expected {expected.ValueKind}, actual {actual.ValueKind}");
                yield break;
            }

            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (ContractDrift drift in CompareObjects(expected, actual, path, policy))
                    {
                        yield return drift;
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (ContractDrift drift in CompareArrays(expected, actual, path, policy))
                    {
                        yield return drift;
                    }

                    break;
                case JsonValueKind.String:
                    if (!StringComparer.Ordinal.Equals(expected.GetString(), actual.GetString()))
                    {
                        yield return ContractDrift.Structural(
                            ContractDriftKind.ValueChanged,
                            path,
                            $"expected {expected.GetRawText()}, actual {actual.GetRawText()}");
                    }

                    break;
                case JsonValueKind.Number:
                    if (!StringComparer.Ordinal.Equals(expected.GetRawText(), actual.GetRawText()))
                    {
                        yield return ContractDrift.Structural(
                            ContractDriftKind.ValueChanged,
                            path,
                            $"expected {expected.GetRawText()}, actual {actual.GetRawText()}");
                    }

                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (expected.GetBoolean() != actual.GetBoolean())
                    {
                        yield return ContractDrift.Structural(
                            ContractDriftKind.ValueChanged,
                            path,
                            $"expected {expected.GetBoolean()}, actual {actual.GetBoolean()}");
                    }

                    break;
                case JsonValueKind.Null:
                    break;
                default:
                    if (!StringComparer.Ordinal.Equals(expected.GetRawText(), actual.GetRawText()))
                    {
                        yield return ContractDrift.Structural(
                            ContractDriftKind.ValueChanged,
                            path,
                            $"expected {expected.GetRawText()}, actual {actual.GetRawText()}");
                    }

                    break;
            }
        }

        private static IEnumerable<ContractDrift> CompareObjects(
            JsonElement expected,
            JsonElement actual,
            string path,
            ContractDriftPolicy policy)
        {
            Dictionary<string, JsonElement> expectedProperties = expected.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value);
            Dictionary<string, JsonElement> actualProperties = actual.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value);

            foreach (string missing in expectedProperties.Keys.Except(actualProperties.Keys, StringComparer.Ordinal))
            {
                yield return ContractDrift.Structural(
                    ContractDriftKind.MissingField,
                    BuildPropertyPath(path, missing),
                    "field exists in the fixture but not in backend serialization");
            }

            foreach (string unexpected in actualProperties.Keys.Except(expectedProperties.Keys, StringComparer.Ordinal))
            {
                string unexpectedPath = BuildPropertyPath(path, unexpected);
                if (!policy.IsReviewedCompatibilityAddition(unexpectedPath))
                {
                    yield return ContractDrift.CompatibilityReview(
                        ContractDriftKind.UnexpectedField,
                        unexpectedPath,
                        "backend serialization added a field that requires fixture and consumer review");
                }
            }

            foreach ((string name, JsonElement expectedValue) in expectedProperties)
            {
                if (actualProperties.TryGetValue(name, out JsonElement actualValue))
                {
                    foreach (ContractDrift drift in Compare(expectedValue, actualValue, BuildPropertyPath(path, name), policy))
                    {
                        yield return drift;
                    }
                }
            }
        }

        private static IEnumerable<ContractDrift> CompareArrays(
            JsonElement expected,
            JsonElement actual,
            string path,
            ContractDriftPolicy policy)
        {
            JsonElement[] expectedItems = expected.EnumerateArray().ToArray();
            JsonElement[] actualItems = actual.EnumerateArray().ToArray();
            if (expectedItems.Length != actualItems.Length)
            {
                yield return ContractDrift.Structural(
                    ContractDriftKind.ArrayLengthChanged,
                    path,
                    $"expected {expectedItems.Length}, actual {actualItems.Length}");
                yield break;
            }

            for (int i = 0; i < expectedItems.Length; i++)
            {
                foreach (ContractDrift drift in Compare(expectedItems[i], actualItems[i], $"{path}[{i}]", policy))
                {
                    yield return drift;
                }
            }
        }

        private static string BuildPropertyPath(string path, string propertyName)
        {
            return $"{path}.{propertyName}";
        }
    }

    private sealed class ContractOracleDriftException(string contractName, IReadOnlyList<ContractDrift> drifts)
        : Exception(CreateMessage(contractName, drifts))
    {
        private static string CreateMessage(string contractName, IReadOnlyList<ContractDrift> drifts)
        {
            string details = string.Join(
                Environment.NewLine,
                drifts.Select(drift => $"- {drift.Category} {drift.Kind} at {drift.Path}: {drift.Message}"));

            bool hasStructuralDrift = drifts.Any(drift => drift.Category == ContractDriftCategory.Structural);
            bool hasCompatibilityReviewDrift = drifts.Any(drift => drift.Category == ContractDriftCategory.CompatibilityReview);
            string summary = (hasStructuralDrift, hasCompatibilityReviewDrift) switch
            {
                (true, true) => "structural drift and compatibility review drift",
                (true, false) => "structural drift",
                (false, true) => "compatibility review drift",
                _ => "contract drift"
            };

            return $"{contractName} Contract Oracle detected {summary}:{Environment.NewLine}{details}";
        }
    }

    private sealed class ContractDriftPolicy
    {
        private readonly HashSet<string> _reviewedCompatibilityAdditions;

        private ContractDriftPolicy(string contractName, IEnumerable<string> reviewedCompatibilityAdditions)
        {
            ContractName = contractName;
            _reviewedCompatibilityAdditions = new HashSet<string>(
                reviewedCompatibilityAdditions,
                StringComparer.Ordinal);
        }

        public string ContractName { get; }

        public static ContractDriftPolicy NoCompatibilityDriftAllowed(string contractName)
        {
            return new ContractDriftPolicy(contractName, []);
        }

        public static ContractDriftPolicy WithReviewedCompatibilityAdditions(
            string contractName,
            params string[] reviewedCompatibilityAdditions)
        {
            return new ContractDriftPolicy(contractName, reviewedCompatibilityAdditions);
        }

        public bool IsReviewedCompatibilityAddition(string path)
        {
            return _reviewedCompatibilityAdditions.Contains(path);
        }
    }

    private sealed record ContractDrift(
        ContractDriftCategory Category,
        ContractDriftKind Kind,
        string Path,
        string Message)
    {
        public static ContractDrift Structural(ContractDriftKind kind, string path, string message)
        {
            return new ContractDrift(ContractDriftCategory.Structural, kind, path, message);
        }

        public static ContractDrift CompatibilityReview(ContractDriftKind kind, string path, string message)
        {
            return new ContractDrift(ContractDriftCategory.CompatibilityReview, kind, path, message);
        }
    }

    private enum ContractDriftCategory
    {
        Structural,
        CompatibilityReview
    }

    private enum ContractDriftKind
    {
        MissingField,
        UnexpectedField,
        ValueKindChanged,
        ValueChanged,
        ArrayLengthChanged
    }
}
