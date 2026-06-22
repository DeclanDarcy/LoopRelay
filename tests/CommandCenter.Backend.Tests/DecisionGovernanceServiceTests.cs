using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionGovernanceServiceTests
{
    [Fact]
    public async Task GenerateReportPersistsAdvisoryGovernanceArtifact()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        Decision decision = CreateResolvedDecision(repository.Id);
        await decisionRepository.SaveDecisionAsync(repository, decision);
        await ProjectAllAsync(repository, decisionRepository, store);
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GenerateReportAsync(repository.Id);

        Assert.StartsWith("governance.", report.Id, StringComparison.Ordinal);
        Assert.Equal(DecisionHealthAssessment.Healthy, report.Health);
        Assert.Equal(1, report.Summary.DecisionCount);
        Assert.Equal(0, report.Summary.BlockingFindingCount);
        Assert.True(File.Exists(Path.Combine(
            repository.Path,
            ".agents",
            "decisions",
            "governance",
            $"{report.Id}.json")));
        Assert.Single(await decisionRepository.ListGovernanceReportsAsync(repository));
    }

    [Fact]
    public async Task ResolvedDecisionWithoutResolutionCreatesBlockingAuthorityFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveDecisionAsync(
            repository,
            CreateResolvedDecision(repository.Id) with
            {
                Resolution = null,
                History = []
            });
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.Blocked, report.Health);
        DecisionGovernanceFinding finding = Assert.Single(report.Findings);
        Assert.Equal(DecisionGovernanceCategory.AuthorityMetadata, finding.Category);
        Assert.Equal(DecisionGovernanceSeverity.Blocking, finding.Severity);
        Assert.True(finding.BlocksExecutionProjection);
        Assert.Contains("no resolution metadata", finding.Detail);
        Assert.Equal(["DEC-0001"], finding.RelatedDecisionIds);
    }

    [Fact]
    public async Task GovernanceDoesNotMutateLifecycleArtifactsWhenOnlyReadingCurrentReport()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        Decision decision = CreateResolvedDecision(repository.Id);
        await decisionRepository.SaveDecisionAsync(repository, decision);
        await ProjectAllAsync(repository, decisionRepository, store);
        string before = await File.ReadAllTextAsync(Path.Combine(
            repository.Path,
            ".agents",
            "decisions",
            "records",
            "DEC-0001",
            "decision.json"));
        var service = CreateService(repository, decisionRepository);

        await service.GetCurrentReportAsync(repository.Id);

        string after = await File.ReadAllTextAsync(Path.Combine(
            repository.Path,
            ".agents",
            "decisions",
            "records",
            "DEC-0001",
            "decision.json"));
        Assert.Equal(before, after);
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "decisions", "governance")));
    }

    [Fact]
    public async Task MissingDecisionMarkdownProjectionCreatesBlockingProjectionFinding()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveDecisionAsync(repository, CreateResolvedDecision(repository.Id));
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.Blocked, report.Health);
        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.ProjectionIntegrity &&
            finding.Title == "Decision DEC-0001 markdown projection is missing" &&
            finding.BlocksExecutionProjection &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001"]));
    }

    [Fact]
    public async Task ConflictingResolvedDecisionsCreateBlockingConsistencyFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision first = CreateResolvedDecision(repository.Id);
        Decision second = CreateResolvedDecision(repository.Id, "DEC-0002", "Use local state") with
        {
            Relationships =
            [
                new DecisionRelationship(
                    new DecisionId("DEC-0002"),
                    new DecisionId("DEC-0001"),
                    DecisionRelationshipType.ConflictsWith,
                    "Directions cannot both be active.")
            ]
        };
        await decisionRepository.SaveDecisionAsync(repository, first);
        await decisionRepository.SaveDecisionAsync(repository, second);
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.Consistency &&
            finding.BlocksExecutionProjection &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001", "DEC-0002"]));
    }

    [Fact]
    public async Task PromotedCandidateWithoutProposalCreatesCoverageFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id));
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.AdvisoryFindings, report.Health);
        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.DecisionCoverage &&
            finding.Severity == DecisionGovernanceSeverity.Warning &&
            finding.RelatedCandidateIds.SequenceEqual(["CAND-0001"]));
    }

    [Fact]
    public async Task ActiveProposalForTerminalCandidateCreatesStaleProposalFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveCandidateAsync(
            repository,
            CreateCandidate(repository.Id, state: DecisionCandidateState.Expired));
        await decisionRepository.SaveProposalAsync(repository, CreateProposal(repository.Id));
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.AdvisoryFindings, report.Health);
        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.ProposalQuality &&
            finding.Title == "Unresolved proposal source candidate is no longer active" &&
            finding.Severity == DecisionGovernanceSeverity.Warning &&
            !finding.BlocksExecutionProjection &&
            finding.RelatedCandidateIds.SequenceEqual(["CAND-0001"]) &&
            finding.RelatedProposalIds.SequenceEqual(["PROP-0001"]));
    }

    [Fact]
    public async Task ActiveProposalForResolvedCandidateCreatesStaleProposalFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id));
        await decisionRepository.SaveProposalAsync(repository, CreateProposal(repository.Id));
        await decisionRepository.SaveDecisionAsync(
            repository,
            CreateResolvedDecision(
                repository.Id,
                proposalId: "PROP-0002"));
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.AdvisoryFindings, report.Health);
        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.ProposalQuality &&
            finding.Title == "Unresolved proposal is stale after candidate resolution" &&
            finding.Severity == DecisionGovernanceSeverity.Warning &&
            !finding.BlocksExecutionProjection &&
            finding.RelatedCandidateIds.SequenceEqual(["CAND-0001"]) &&
            finding.RelatedProposalIds.SequenceEqual(["PROP-0001"]));
    }

    [Fact]
    public async Task MultipleActiveCandidatesWithSameSourceFingerprintCreateStaleCandidateFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id));
        await decisionRepository.SaveCandidateAsync(
            repository,
            CreateCandidate(
                repository.Id,
                candidateId: "CAND-0002",
                state: DecisionCandidateState.Discovered));
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.AdvisoryFindings, report.Health);
        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.DecisionCoverage &&
            finding.Title == "Multiple active candidates share a source fingerprint" &&
            finding.Severity == DecisionGovernanceSeverity.Warning &&
            !finding.BlocksExecutionProjection &&
            finding.RelatedCandidateIds.SequenceEqual(["CAND-0001", "CAND-0002"]));
    }

    [Fact]
    public async Task ActiveCandidateWithTerminalSourceFingerprintCreatesStaleCandidateFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id));
        await decisionRepository.SaveCandidateAsync(
            repository,
            CreateCandidate(
                repository.Id,
                candidateId: "CAND-0002",
                state: DecisionCandidateState.Duplicate));
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.AdvisoryFindings, report.Health);
        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.DecisionCoverage &&
            finding.Title == "Active candidate reuses terminal source fingerprint" &&
            finding.Severity == DecisionGovernanceSeverity.Warning &&
            !finding.BlocksExecutionProjection &&
            finding.RelatedCandidateIds.SequenceEqual(["CAND-0001", "CAND-0002"]));
    }

    [Fact]
    public async Task ActiveCandidateWithResolvedAuthorityCreatesStaleCandidateFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id));
        await decisionRepository.SaveProposalAsync(
            repository,
            CreateProposal(repository.Id, state: DecisionProposalState.Resolved));
        await decisionRepository.SaveDecisionAsync(repository, CreateResolvedDecision(repository.Id));
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.AdvisoryFindings, report.Health);
        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.DecisionCoverage &&
            finding.Title == "Active candidate already has resolved authority" &&
            finding.Severity == DecisionGovernanceSeverity.Warning &&
            !finding.BlocksExecutionProjection &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001"]) &&
            finding.RelatedCandidateIds.SequenceEqual(["CAND-0001"]) &&
            finding.RelatedProposalIds.SequenceEqual(["PROP-0001"]));
    }

    [Fact]
    public async Task SupersededDecisionWithoutIncomingSupersedesCreatesLineageFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveDecisionAsync(
            repository,
            CreateResolvedDecision(repository.Id) with
            {
                State = DecisionState.Superseded
            });
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.SupersessionLineage &&
            finding.Title == "Superseded decision has no replacement ancestry" &&
            finding.BlocksExecutionProjection &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001"]));
    }

    [Fact]
    public async Task SupersededDecisionWithMultipleParentsCreatesLineageFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision superseded = CreateResolvedDecision(repository.Id) with
        {
            State = DecisionState.Superseded
        };
        Decision firstReplacement = CreateResolvedDecision(repository.Id, "DEC-0002", "Use file-backed authority") with
        {
            Relationships =
            [
                new DecisionRelationship(
                    new DecisionId("DEC-0002"),
                    new DecisionId("DEC-0001"),
                    DecisionRelationshipType.Supersedes,
                    "First replacement.")
            ]
        };
        Decision secondReplacement = CreateResolvedDecision(repository.Id, "DEC-0003", "Use service-backed authority") with
        {
            Relationships =
            [
                new DecisionRelationship(
                    new DecisionId("DEC-0003"),
                    new DecisionId("DEC-0001"),
                    DecisionRelationshipType.Supersedes,
                    "Second replacement.")
            ]
        };
        await decisionRepository.SaveDecisionAsync(repository, superseded);
        await decisionRepository.SaveDecisionAsync(repository, firstReplacement);
        await decisionRepository.SaveDecisionAsync(repository, secondReplacement);
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.SupersessionLineage &&
            finding.Title == "Superseded decision has multiple replacement parents" &&
            finding.BlocksExecutionProjection &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001", "DEC-0002", "DEC-0003"]));
    }

    [Fact]
    public async Task RelationshipToInactiveAuthorityCreatesBoundaryFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision archivedAuthority = CreateResolvedDecision(repository.Id, "DEC-0001", "Use archived authority") with
        {
            State = DecisionState.Archived
        };
        Decision dependent = CreateResolvedDecision(repository.Id, "DEC-0002", "Use dependent authority") with
        {
            Relationships =
            [
                new DecisionRelationship(
                    new DecisionId("DEC-0002"),
                    new DecisionId("DEC-0001"),
                    DecisionRelationshipType.DependsOn,
                    "Dependency should be active.")
            ]
        };
        await decisionRepository.SaveDecisionAsync(repository, archivedAuthority);
        await decisionRepository.SaveDecisionAsync(repository, dependent);
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.AuthorityBoundary &&
            finding.Title == "Relationship references inactive authority" &&
            finding.BlocksExecutionProjection &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001", "DEC-0002"]));
    }

    [Fact]
    public async Task MultipleAcceptedDecisionsForOneCandidateCreateBoundaryFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveDecisionAsync(repository, CreateResolvedDecision(repository.Id, "DEC-0001", "Use first authority"));
        await decisionRepository.SaveDecisionAsync(repository, CreateResolvedDecision(repository.Id, "DEC-0002", "Use second authority"));
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.AuthorityBoundary &&
            finding.Title == "Multiple active authorities for one candidate" &&
            finding.BlocksExecutionProjection &&
            finding.RelatedCandidateIds.SequenceEqual(["CAND-0001"]) &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001", "DEC-0002"]));
    }

    [Fact]
    public async Task ConflictingExecutionDirectivesCreateBlockingReadinessFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveDecisionAsync(
            repository,
            CreateResolvedDecision(
                repository.Id,
                "DEC-0001",
                "Enable automatic assimilation",
                candidateId: "CAND-0001",
                proposalId: "PROP-0001",
                selectedOptionTitle: "Enable automatic assimilation"));
        await decisionRepository.SaveDecisionAsync(
            repository,
            CreateResolvedDecision(
                repository.Id,
                "DEC-0002",
                "Disable automatic assimilation",
                candidateId: "CAND-0002",
                proposalId: "PROP-0002",
                selectedOptionTitle: "Disable automatic assimilation"));
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.Blocked, report.Health);
        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.ExecutionProjectionReadiness &&
            finding.Title == "Conflicting execution directives" &&
            finding.BlocksExecutionProjection &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001", "DEC-0002"]) &&
            finding.RelatedCandidateIds.SequenceEqual(["CAND-0001", "CAND-0002"]) &&
            finding.RelatedProposalIds.SequenceEqual(["PROP-0001", "PROP-0002"]));
    }

    [Fact]
    public async Task NonAcceptedResolvedDecisionCreatesAdvisoryReadinessFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateResolvedDecision(repository.Id) with
        {
            Resolution = CreateResolvedDecision(repository.Id).Resolution! with
            {
                Outcome = DecisionOutcome.Deferred
            }
        };
        await decisionRepository.SaveDecisionAsync(repository, decision);
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Equal(DecisionHealthAssessment.AdvisoryFindings, report.Health);
        DecisionGovernanceFinding finding = Assert.Single(report.Findings);
        Assert.Equal(DecisionGovernanceCategory.ExecutionProjectionReadiness, finding.Category);
        Assert.Equal(DecisionGovernanceSeverity.Warning, finding.Severity);
        Assert.False(finding.BlocksExecutionProjection);
        Assert.Contains("should not be projected", finding.Detail);
    }

    [Fact]
    public async Task InvalidResolvedSnapshotFingerprintCreatesBlockingFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateResolvedDecision(repository.Id) with
        {
            Resolution = CreateResolvedDecision(repository.Id).Resolution! with
            {
                SourceProposalSnapshot = CreateResolvedDecision(repository.Id).Resolution!.SourceProposalSnapshot! with
                {
                    ProposalFingerprint = "stale-fingerprint"
                }
            }
        };
        await decisionRepository.SaveDecisionAsync(repository, decision);
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.FingerprintIntegrity &&
            finding.Title == "Resolved decision source proposal fingerprint is invalid" &&
            finding.BlocksExecutionProjection &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001"]));
    }

    [Fact]
    public async Task IncompleteResolvedSnapshotCreatesBlockingFinding()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateResolvedDecision(repository.Id);
        await decisionRepository.SaveDecisionAsync(
            repository,
            decision with
            {
                Resolution = decision.Resolution! with
                {
                    SourceProposalSnapshot = decision.Resolution.SourceProposalSnapshot! with
                    {
                        Options = []
                    }
                }
            });
        var service = CreateService(repository, decisionRepository);

        DecisionGovernanceReport report = await service.GetCurrentReportAsync(repository.Id);

        Assert.Contains(report.Findings, finding =>
            finding.Category == DecisionGovernanceCategory.FingerprintIntegrity &&
            finding.Title == "Resolved decision source proposal snapshot is incomplete" &&
            finding.BlocksExecutionProjection &&
            finding.RelatedDecisionIds.SequenceEqual(["DEC-0001"]));
    }

    private static DecisionGovernanceService CreateService(
        Repository repository,
        InMemoryDecisionRepository decisionRepository)
    {
        return new DecisionGovernanceService(new StubRepositoryService(repository), decisionRepository);
    }

    private static DecisionGovernanceService CreateService(
        Repository repository,
        FileSystemDecisionRepository decisionRepository)
    {
        return new DecisionGovernanceService(new StubRepositoryService(repository), decisionRepository);
    }

    private static Decision CreateResolvedDecision(
        Guid repositoryId,
        string decisionId = "DEC-0001",
        string title = "Use repository-backed decisions",
        string candidateId = "CAND-0001",
        string proposalId = "PROP-0001",
        string selectedOptionTitle = "Use repository-backed decisions",
        string selectedOptionDescription = "Persist records under .agents/decisions.")
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var id = new DecisionId(decisionId);
        var proposal = new DecisionProposal(
            proposalId,
            repositoryId,
            candidateId,
            DecisionProposalState.ReadyForResolution,
            title,
            "Decision lifecycle state must be recoverable from repository artifacts.",
            [new DecisionOption("option-1", selectedOptionTitle, selectedOptionDescription, [])],
            [new DecisionTradeoff("option-1", "Recoverable.", "Requires schema discipline.", [])],
            new DecisionRecommendation("option-1", "Matches repository authority.", []),
            [],
            [new DecisionEvidence("Plan requires repository authority.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            []);
        var snapshot = new DecisionResolvedProposalSnapshot(
            proposal.Id,
            proposal.CandidateId,
            Fingerprint(proposal),
            proposal.State,
            proposal.Title,
            proposal.Context,
            proposal.Options,
            proposal.Tradeoffs,
            proposal.Recommendation,
            proposal.Assumptions,
            proposal.Evidence,
            proposal.History,
            []);
        return new Decision(
            id,
            DecisionState.Resolved,
            DecisionClassification.Architectural,
            title,
            "Decision lifecycle state must be recoverable from repository artifacts.",
            new DecisionMetadata(repositoryId, now, now),
            new DecisionResolution(
                DecisionOutcome.Accepted,
                "option-1",
                "Repository artifacts are the authoritative source.",
                "human-reviewer",
                false,
                now,
                [new DecisionSourceReference("DecisionProposal", $".agents/decisions/proposals/{proposalId}/proposal.json", DecisionId: id, ProposalId: proposalId)],
                snapshot),
            [],
            [new DecisionEvidence("Plan requires repository authority.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            [new DecisionHistoryEntry(now, "Resolved", DecisionState.Open.ToString(), DecisionState.Resolved.ToString(), "Resolved by test.", [])]);
    }

    private static string Fingerprint(DecisionProposal proposal)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(proposal, CreateJsonOptions()));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static async Task ProjectAllAsync(
        Repository repository,
        FileSystemDecisionRepository decisionRepository,
        FileSystemArtifactStore store)
    {
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        await projectionService.RefreshAllAsync(repository);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static DecisionCandidate CreateCandidate(
        Guid repositoryId,
        string candidateId = "CAND-0001",
        DecisionCandidateState state = DecisionCandidateState.Promoted)
    {
        return new DecisionCandidate(
            candidateId,
            repositoryId,
            state,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Decide persistence schema",
            "Need to decide repository-backed persistence schema.",
            "source-fingerprint",
            [],
            [new DecisionEvidence("Plan requires a persistence decision.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            [new DecisionSourceReference("Plan", ".agents/plan.md")],
            [],
            [new DecisionHistoryEntry(DateTimeOffset.UtcNow, state.ToString(), null, state.ToString(), "Seeded by governance test.", [])]);
    }

    private static DecisionProposal CreateProposal(
        Guid repositoryId,
        string proposalId = "PROP-0001",
        string candidateId = "CAND-0001",
        DecisionProposalState state = DecisionProposalState.Generated)
    {
        return new DecisionProposal(
            proposalId,
            repositoryId,
            candidateId,
            state,
            "Persist decisions as structured JSON",
            "Decision lifecycle state must be reviewable before resolution.",
            [new DecisionOption("option-1", "Use structured files", "Persist lifecycle records under .agents/decisions.", [])],
            [new DecisionTradeoff("option-1", "Recoverable from repository artifacts.", "Requires schema validation.", [])],
            new DecisionRecommendation("option-1", "Matches repository authority.", []),
            [],
            [new DecisionEvidence("Plan requires repository authority.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            [new DecisionHistoryEntry(DateTimeOffset.UtcNow, state.ToString(), null, state.ToString(), "Seeded by governance test.", [])]);
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, ".git"));
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path
        };
    }

    private sealed class StubRepositoryService(params Repository[] repositories) : IRepositoryService
    {
        public Task<IReadOnlyList<Repository>> GetAllAsync()
        {
            return Task.FromResult<IReadOnlyList<Repository>>(repositories);
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
}
