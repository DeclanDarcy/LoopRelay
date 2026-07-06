using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;
using LoopRelay.Decisions.Services;

namespace LoopRelay.Decisions.Tests;

public sealed class DecisionProjectionServiceTests
{
    [Fact]
    public async Task ProjectionIncludesOnlyAcceptedResolvedDecisions()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(harness.Repository.Id, "DEC-0001", DecisionState.Resolved, DecisionOutcome.Accepted, DecisionClassification.Architectural));
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(harness.Repository.Id, "DEC-0002", DecisionState.UnderReview, DecisionOutcome.Accepted, DecisionClassification.Tactical));
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(harness.Repository.Id, "DEC-0003", DecisionState.Resolved, DecisionOutcome.Deferred, DecisionClassification.Tactical));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        ExecutionConstraint constraint = Assert.Single(projection.Constraints);
        Assert.Equal("DEC-0001", constraint.DecisionId);
        Assert.Equal("Use repository artifacts", constraint.Statement);
        Assert.Equal(ExecutionProjectionKind.RepositoryConvention, constraint.ProjectionKind);
        Assert.Empty(projection.Directives);
    }

    [Fact]
    public async Task ProjectionExcludesDecisionsWithBlockingGovernanceFindings()
    {
        Harness harness = CreateHarness(
            new DecisionGovernanceFinding(
                "GOV-0001",
                DecisionGovernanceCategory.AuthorityMetadata,
                DecisionGovernanceSeverity.Blocking,
                true,
                "Blocked",
                "Blocked",
                [],
                ["DEC-0001"],
                [],
                []));
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(harness.Repository.Id, "DEC-0001", DecisionState.Resolved, DecisionOutcome.Accepted, DecisionClassification.Operational));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        Assert.Empty(projection.Constraints);
        Assert.Empty(projection.Directives);
        DecisionProjectionDecisionDiagnostic blockedDecision = Assert.Single(projection.BlockedDecisions);
        Assert.Equal("DEC-0001", blockedDecision.DecisionId);
        Assert.Contains("Blocking governance finding", blockedDecision.Reason, StringComparison.Ordinal);
        Assert.Contains(projection.ExcludedDecisions, decision => decision.DecisionId == "DEC-0001");
        Assert.Contains(projection.Diagnostics, diagnostic => diagnostic.Contains("DEC-0001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TacticalResolvedDecisionsProjectAsDirectives()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0001",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Tactical,
                "Apply implementation patch",
                "Apply the implementation patch for the current milestone."));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        ExecutionDirective directive = Assert.Single(projection.Directives);
        Assert.Equal("DEC-0001", directive.DecisionId);
        Assert.Equal(DecisionClassification.Tactical, directive.Classification);
        Assert.Equal("Apply implementation patch: Apply the implementation patch for the current milestone.", directive.Statement);
        Assert.Equal(ExecutionProjectionKind.ImplementationDirective, directive.ProjectionKind);
        Assert.Empty(projection.Constraints);
    }

    [Fact]
    public async Task ProjectionClassifiesTechnologyChoicesAsConstraints()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0001",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Tactical,
                "Use React framework",
                "Use React framework for decision lifecycle UI surfaces."));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        ExecutionConstraint constraint = Assert.Single(projection.Constraints);
        Assert.Equal("DEC-0001", constraint.DecisionId);
        Assert.Equal(ExecutionProjectionKind.TechnologyChoice, constraint.ProjectionKind);
        Assert.Empty(projection.Directives);
    }

    [Fact]
    public async Task ProjectionClassifiesWorkflowPoliciesAsDirectives()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0001",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Strategic,
                "Require review workflow",
                "Require review before mutating lifecycle authority."));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        ExecutionDirective directive = Assert.Single(projection.Directives);
        Assert.Equal("DEC-0001", directive.DecisionId);
        Assert.Equal(ExecutionProjectionKind.WorkflowPolicy, directive.ProjectionKind);
        ExecutionDecisionPriority priority = Assert.Single(projection.Priorities);
        Assert.Equal("DEC-0001", priority.DecisionId);
        Assert.Equal(1, priority.Rank);
        Assert.Empty(projection.Constraints);
    }

    [Fact]
    public async Task ProjectionClassifiesRepositoryConventionsAsConstraints()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0001",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Operational,
                "Preserve .agents layout",
                "Preserve .agents decision artifact layout."));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        ExecutionConstraint constraint = Assert.Single(projection.Constraints);
        Assert.Equal("DEC-0001", constraint.DecisionId);
        Assert.Equal(ExecutionProjectionKind.RepositoryConvention, constraint.ProjectionKind);
        ExecutionArchitectureRule rule = Assert.Single(projection.ArchitectureRules);
        Assert.Equal("DEC-0001", rule.DecisionId);
        Assert.Empty(projection.Directives);
    }

    [Fact]
    public async Task ProjectionExcludesSupersededAuthorityAndProjectsReplacement()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(harness.Repository.Id, "DEC-0001", DecisionState.Superseded, DecisionOutcome.Accepted, DecisionClassification.Architectural));
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0002",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Architectural,
                "Use current repository layout",
                relationships:
                [
                    new DecisionRelationship(
                        new DecisionId("DEC-0002"),
                        new DecisionId("DEC-0001"),
                        DecisionRelationshipType.Supersedes,
                        "Replacement authority.")
                ]));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        ExecutionConstraint constraint = Assert.Single(projection.Constraints);
        Assert.Equal("DEC-0002", constraint.DecisionId);
        DecisionProjectionDecisionDiagnostic supersededDecision = Assert.Single(projection.SupersededDecisions);
        Assert.Equal("DEC-0001", supersededDecision.DecisionId);
        Assert.Contains("DEC-0002", supersededDecision.Reason, StringComparison.Ordinal);
        Assert.Contains(projection.Diagnostics, diagnostic => diagnostic.Contains("DEC-0001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProjectionDetectsContradictoryProjectedDirectives()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0001",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Tactical,
                "Use provider abstraction"));
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0002",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Tactical,
                "Avoid provider abstraction"));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        ExecutionDecisionConflict conflict = Assert.Single(projection.Conflicts);
        Assert.Equal("DEC-0001", conflict.DecisionId);
        Assert.Contains("DEC-0002", conflict.ConflictingExcerpt, StringComparison.Ordinal);
        Assert.Contains(projection.ConflictingDecisions, decision =>
            decision.DecisionId == "DEC-0001" &&
            decision.Reason.Contains("DEC-0002", StringComparison.Ordinal));
        Assert.Contains(projection.ConflictingDecisions, decision =>
            decision.DecisionId == "DEC-0002" &&
            decision.Reason.Contains("DEC-0001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProjectionDetectsMutuallyExclusiveArchitectureRules()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0001",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Architectural,
                "Use React framework"));
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0002",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Architectural,
                "Use Vue framework"));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        ExecutionDecisionConflict conflict = Assert.Single(projection.Conflicts);
        Assert.Equal("DEC-0001", conflict.DecisionId);
        Assert.Contains("DEC-0002", conflict.ConflictingExcerpt, StringComparison.Ordinal);
        Assert.Equal(2, projection.ArchitectureRules.Count);
    }

    [Fact]
    public async Task ProjectionExcludesSupersededAuthorityWhenStateIsStale()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0001",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Architectural,
                "Use legacy repository layout"));
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(
                harness.Repository.Id,
                "DEC-0002",
                DecisionState.Resolved,
                DecisionOutcome.Accepted,
                DecisionClassification.Architectural,
                "Use current repository layout",
                relationships:
                [
                    new DecisionRelationship(
                        new DecisionId("DEC-0002"),
                        new DecisionId("DEC-0001"),
                        DecisionRelationshipType.Supersedes,
                        "Replacement authority.")
                ]));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        ExecutionConstraint constraint = Assert.Single(projection.Constraints);
        Assert.Equal("DEC-0002", constraint.DecisionId);
        Assert.Contains(projection.Diagnostics, diagnostic =>
            diagnostic.Contains("DEC-0001", StringComparison.Ordinal) &&
            diagnostic.Contains("state is Resolved", StringComparison.Ordinal));
        Assert.Contains(projection.Context.Diagnostics, diagnostic =>
            diagnostic.Contains("DEC-0001", StringComparison.Ordinal) &&
            diagnostic.Contains("DEC-0002", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProjectionDetectsMilestoneConflictsWithGovernedDecisionStatements()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(harness.Repository.Id, "DEC-0001", DecisionState.Resolved, DecisionOutcome.Accepted, DecisionClassification.Operational));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(
            harness.Repository.Id,
            milestoneContent: "This slice should avoid repository artifacts.");

        ExecutionDecisionConflict conflict = Assert.Single(projection.Conflicts);
        Assert.Equal("DEC-0001", conflict.DecisionId);
        Assert.Equal("avoid repository artifacts", conflict.ConflictingExcerpt);
    }

    [Fact]
    public async Task ProjectionDoesNotIncludeUnresolvedProposals()
    {
        Harness harness = CreateHarness();
        await harness.DecisionRepository.SaveProposalAsync(
            harness.Repository,
            new DecisionProposal(
                "PROP-0001",
                harness.Repository.Id,
                "CAND-0001",
                DecisionProposalState.ReadyForResolution,
                "Unresolved proposal",
                "This proposal has not been resolved by a human.",
                [new DecisionOption("OPT-0001", "Use unresolved proposal text", string.Empty, [])],
                [],
                new DecisionRecommendation("OPT-0001", "Use unresolved proposal text.", []),
                [],
                [],
                []));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        Assert.Empty(projection.Constraints);
        Assert.Empty(projection.Directives);
    }

    [Fact]
    public async Task ProjectionPersistsExecutionDiagnosticsArtifact()
    {
        Harness harness = CreateHarness(withArtifactStore: true);
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(harness.Repository.Id, "DEC-0001", DecisionState.Resolved, DecisionOutcome.Accepted, DecisionClassification.Architectural));
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(harness.Repository.Id, "DEC-0002", DecisionState.Superseded, DecisionOutcome.Accepted, DecisionClassification.Tactical));
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            CreateDecision(harness.Repository.Id, "DEC-0003", DecisionState.Resolved, DecisionOutcome.Rejected, DecisionClassification.Operational));

        ExecutionDecisionProjection projection = await harness.Service.BuildExecutionProjectionAsync(harness.Repository.Id);

        string projectionsRoot = Path.Combine(harness.Repository.Path, ".agents", "decisions", "projections");
        string jsonPath = Assert.Single(Directory.GetFiles(projectionsRoot, "execution.*.json"));
        string markdownPath = Assert.Single(Directory.GetFiles(projectionsRoot, "execution.*.md"));
        string json = await File.ReadAllTextAsync(jsonPath);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        DecisionProjectionDiagnostics diagnostics =
            JsonSerializer.Deserialize<DecisionProjectionDiagnostics>(
                root.GetProperty("payload").GetRawText(),
                jsonOptions)!;

        Assert.Equal(harness.Repository.Id.ToString(), root.GetProperty("repositoryId").GetString());
        Assert.Equal(projection.GeneratedAt, diagnostics.GeneratedAt);
        Assert.Equal("DEC-0001", Assert.Single(diagnostics.IncludedDecisions).DecisionId);
        Assert.Contains(diagnostics.ExcludedDecisions, decision => decision.DecisionId == "DEC-0002");
        Assert.Contains(diagnostics.ExcludedDecisions, decision => decision.DecisionId == "DEC-0003");
        Assert.Equal("DEC-0002", Assert.Single(diagnostics.SupersededDecisions).DecisionId);
        Assert.Equal("DEC-0003", Assert.Single(diagnostics.IgnoredDecisions).DecisionId);
        Assert.Empty(diagnostics.BlockedDecisions);
        Assert.Empty(diagnostics.ConflictingDecisions);
        Assert.Contains(diagnostics.ProjectedStatements, statement => statement.ProjectionCategory == "Constraint");
        Assert.Contains(diagnostics.ProjectedStatements, statement => statement.ProjectionCategory == "ArchitectureRule");
        Assert.False(string.IsNullOrWhiteSpace(diagnostics.ProjectionFingerprint));
        Assert.Contains("Included Decisions", await File.ReadAllTextAsync(markdownPath), StringComparison.Ordinal);
    }

    private static Harness CreateHarness(params DecisionGovernanceFinding[] findings)
    {
        return CreateHarness(false, findings);
    }

    private static Harness CreateHarness(bool withArtifactStore, params DecisionGovernanceFinding[] findings)
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "Project",
            Path = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"))
        };
        Directory.CreateDirectory(repository.Path);
        var repositoryService = new StaticRepositoryService(repository);
        var decisionRepository = new InMemoryDecisionRepository();
        var governanceService = new StaticGovernanceService(repository.Id, findings);
        var service = new DecisionProjectionService(
            repositoryService,
            decisionRepository,
            governanceService,
            withArtifactStore ? new FileSystemArtifactStore() : null);
        return new Harness(repository, decisionRepository, service);
    }

    private static Decision CreateDecision(
        Guid repositoryId,
        string decisionId,
        DecisionState state,
        DecisionOutcome outcome,
        DecisionClassification classification,
        string selectedOptionTitle = "Use repository artifacts",
        string selectedOptionDescription = "",
        IReadOnlyList<DecisionRelationship>? relationships = null)
    {
        var id = new DecisionId(decisionId);
        var source = new DecisionSourceReference(
            "DecisionRecord",
            $".agents/decisions/records/{decisionId}/decision.json",
            DecisionId: id);
        var selectedOption = new DecisionOption(
            "OPT-0001",
            selectedOptionTitle,
            selectedOptionDescription,
            [new DecisionEvidence("Repository artifacts are authoritative.", [source])]);
        var snapshot = new DecisionResolvedProposalSnapshot(
            "PROP-0001",
            "CAND-0001",
            "fingerprint",
            DecisionProposalState.ReadyForResolution,
            "Projection source",
            "Projection context",
            [selectedOption],
            [],
            new DecisionRecommendation("OPT-0001", "Use repository artifacts.", []),
            [],
            [new DecisionEvidence("Repository artifacts are authoritative.", [source])],
            [],
            []);

        return new Decision(
            id,
            state,
            classification,
            $"Decision {decisionId}",
            "Decision context",
            new DecisionMetadata(repositoryId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
            new DecisionResolution(outcome, "OPT-0001", "Use repository artifacts.", "user", false, DateTimeOffset.UtcNow, [source], snapshot),
            relationships ?? [],
            [new DecisionEvidence("Repository artifacts are authoritative.", [source])],
            []);
    }

    private sealed record Harness(
        Repository Repository,
        InMemoryDecisionRepository DecisionRepository,
        DecisionProjectionService Service);

    private sealed class StaticRepositoryService(Repository repository) : IRepositoryService
    {
        public Task<IReadOnlyList<Repository>> GetAllAsync()
        {
            return Task.FromResult<IReadOnlyList<Repository>>([repository]);
        }

        public Task<Repository> RegisterAsync(string repositoryPath)
        {
            throw new NotSupportedException();
        }

        public Task RemoveAsync(Guid repositoryId)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StaticGovernanceService(Guid repositoryId, IReadOnlyList<DecisionGovernanceFinding> findings)
        : IDecisionGovernanceService
    {
        public Task<DecisionGovernanceReport> GetCurrentReportAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult(CreateReport(requestedRepositoryId));
        }

        public Task<DecisionGovernanceReport> GenerateReportAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult(CreateReport(requestedRepositoryId));
        }

        public Task<IReadOnlyList<DecisionGovernanceReport>> ListReportsAsync(Guid requestedRepositoryId)
        {
            return Task.FromResult<IReadOnlyList<DecisionGovernanceReport>>([CreateReport(requestedRepositoryId)]);
        }

        private DecisionGovernanceReport CreateReport(Guid requestedRepositoryId)
        {
            Assert.Equal(repositoryId, requestedRepositoryId);
            return new DecisionGovernanceReport(
                "governance.test",
                repositoryId,
                DateTimeOffset.UtcNow,
                "fingerprint",
                findings.Any(finding => finding.BlocksExecutionProjection)
                    ? DecisionHealthAssessment.Blocked
                    : DecisionHealthAssessment.Healthy,
                new DecisionGovernanceSummary(0, 0, 0, 0, 0, findings.Count, findings.Count(finding => finding.BlocksExecutionProjection)),
                findings,
                []);
        }
    }
}
