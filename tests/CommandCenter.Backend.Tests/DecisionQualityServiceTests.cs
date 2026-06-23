using CommandCenter.Backend.Services;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionQualityServiceTests
{
    [Fact]
    public async Task AcceptedRecommendedOptionProducesPositiveQualitySignals()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateDecision(repository.Id, DecisionOutcome.Accepted, "option-1", recommendedOptionId: "option-1");
        await decisionRepository.SaveDecisionAsync(repository, decision);
        IDecisionQualityAssessmentService service = CreateAssessmentService(repository, decisionRepository);

        DecisionQualityAssessment assessment = await service.AssessDecisionAsync(repository.Id, decision.Id.Value);

        Assert.True(assessment.Score > 50);
        Assert.True(assessment.Rating is DecisionQualityRating.Good or DecisionQualityRating.Excellent);
        Assert.Contains(assessment.Signals, signal =>
            signal.Category == "ResolutionOutcome" &&
            signal.Direction == QualitySignalDirection.Positive);
        Assert.Contains(assessment.Signals, signal =>
            signal.Category == "RecommendationQuality" &&
            signal.Direction == QualitySignalDirection.Positive &&
            signal.Summary.Contains("recommended option", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(assessment.HumanAuthoringBurdenSignals, signal =>
            signal.Burden == HumanAuthoringBurden.ReviewOnly);
    }

    [Fact]
    public async Task RejectedDecisionProducesNegativeQualitySignals()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateDecision(repository.Id, DecisionOutcome.Rejected, "option-1", recommendedOptionId: "option-1");
        await decisionRepository.SaveDecisionAsync(repository, decision);
        IDecisionQualityAssessmentService service = CreateAssessmentService(repository, decisionRepository);

        DecisionQualityAssessment assessment = await service.AssessDecisionAsync(repository.Id, decision.Id.Value);

        Assert.Contains(assessment.Signals, signal =>
            signal.Category == "ResolutionOutcome" &&
            signal.Direction == QualitySignalDirection.Negative &&
            signal.Severity == QualitySignalSeverity.High);
        Assert.True(assessment.Rating is DecisionQualityRating.Mixed or DecisionQualityRating.Poor);
    }

    [Fact]
    public async Task AlternativeSelectionLowersRecommendationQualityButPreservesOptionUsefulness()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateDecision(repository.Id, DecisionOutcome.Accepted, "option-2", recommendedOptionId: "option-1");
        await decisionRepository.SaveDecisionAsync(repository, decision);
        IDecisionQualityAssessmentService service = CreateAssessmentService(repository, decisionRepository);

        DecisionQualityAssessment assessment = await service.AssessDecisionAsync(repository.Id, decision.Id.Value);

        Assert.Contains(assessment.Signals, signal =>
            signal.Category == "RecommendationQuality" &&
            signal.Direction == QualitySignalDirection.Negative);
        Assert.Contains(assessment.Signals, signal =>
            signal.Category == "OptionQuality" &&
            signal.Direction == QualitySignalDirection.Positive);
    }

    [Fact]
    public async Task FullRewriteAndGenerationBypassAreRecordedSeparately()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision rewritten = CreateDecision(repository.Id, DecisionOutcome.Accepted, "option-1", recommendedOptionId: "option-1");
        Decision bypassed = CreateDecision(
            repository.Id,
            DecisionOutcome.Accepted,
            "custom-option",
            recommendedOptionId: "option-1",
            decisionId: "DEC-0002",
            includeSnapshot: false);
        await decisionRepository.SaveDecisionAsync(repository, rewritten);
        await decisionRepository.SaveDecisionAsync(repository, bypassed);
        await decisionRepository.SaveProposalRevisionAsync(
            repository,
            new DecisionProposalRevision(
                "REV-0001",
                repository.Id,
                "PROP-0001",
                DateTimeOffset.UtcNow,
                "Reviewer rewrote generated content.",
                ["Options", "Recommendation"],
                "source-fingerprint",
                [ProposalSource("PROP-0001", rewritten.Id)])
            {
                HumanAuthoringBurden = HumanAuthoringBurden.FullRewrite
            });
        IDecisionQualityAssessmentService assessmentService = CreateAssessmentService(repository, decisionRepository);
        IDecisionQualityReportService reportService = CreateReportService(repository, decisionRepository, assessmentService);

        DecisionQualityAssessment rewrittenAssessment =
            await assessmentService.AssessDecisionAsync(repository.Id, rewritten.Id.Value);
        DecisionQualityAssessment bypassedAssessment =
            await assessmentService.AssessDecisionAsync(repository.Id, bypassed.Id.Value);
        DecisionQualityReport report = await reportService.GenerateReportAsync(repository.Id);

        Assert.Contains(rewrittenAssessment.HumanAuthoringBurdenSignals, signal =>
            signal.Burden == HumanAuthoringBurden.FullRewrite);
        Assert.Contains(bypassedAssessment.HumanAuthoringBurdenSignals, signal =>
            signal.Burden == HumanAuthoringBurden.GenerationBypassed);
        Assert.Contains(bypassedAssessment.Signals, signal =>
            signal.Category == "HumanAuthoringBurden" &&
            signal.Severity == QualitySignalSeverity.Critical);
        Assert.Equal(1, report.FullRewriteCount);
        Assert.Equal(1, report.GenerationBypassedCount);
    }

    [Fact]
    public async Task QualityAssessmentDoesNotMutateDecisionProposalOrPackageState()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateDecision(repository.Id, DecisionOutcome.Accepted, "option-1", recommendedOptionId: "option-1");
        DecisionProposal proposal = CreateProposal(repository.Id);
        DecisionPackageVersion package = CreatePackageVersion(repository.Id);
        await decisionRepository.SaveDecisionAsync(repository, decision);
        await decisionRepository.SaveProposalAsync(repository, proposal);
        await decisionRepository.SavePackageVersionAsync(repository, package);
        IDecisionQualityAssessmentService service = CreateAssessmentService(repository, decisionRepository);

        await service.AssessDecisionAsync(repository.Id, decision.Id.Value);

        Decision? reloadedDecision = await decisionRepository.GetDecisionAsync(repository, decision.Id);
        DecisionProposal? reloadedProposal = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        DecisionPackageVersion? reloadedPackage =
            await decisionRepository.GetPackageVersionAsync(repository, proposal.Id, package.Id);
        Assert.Equal(decision, reloadedDecision);
        Assert.Equal(proposal, reloadedProposal);
        Assert.Equal(package, reloadedPackage);
    }

    private static IDecisionQualityAssessmentService CreateAssessmentService(
        Repository repository,
        IDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var burdenService = new HumanAuthoringBurdenService(repositoryService, decisionRepository);
        var signalService = new DecisionQualitySignalService(repositoryService, decisionRepository, burdenService);
        return new DecisionQualityAssessmentService(repositoryService, decisionRepository, signalService, burdenService);
    }

    private static IDecisionQualityReportService CreateReportService(
        Repository repository,
        IDecisionRepository decisionRepository,
        IDecisionQualityAssessmentService assessmentService)
    {
        return new DecisionQualityReportService(new StubRepositoryService(repository), decisionRepository, assessmentService);
    }

    private static Decision CreateDecision(
        Guid repositoryId,
        DecisionOutcome outcome,
        string selectedOptionId,
        string recommendedOptionId,
        string decisionId = "DEC-0001",
        bool includeSnapshot = true)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var id = new DecisionId(decisionId);
        DecisionResolvedProposalSnapshot? snapshot = includeSnapshot
            ? new DecisionResolvedProposalSnapshot(
                "PROP-0001",
                "CAND-0001",
                "proposal-fingerprint",
                DecisionProposalState.ReadyForResolution,
                "Choose decision quality path",
                "Decide how to assess generated decisions.",
                Options(),
                [],
                new DecisionRecommendation(
                    recommendedOptionId,
                    "Recommended from generated evidence.",
                    [])
                {
                    Summary = "Prefer generated path."
                },
                [],
                [],
                [],
                [])
            {
                PackageId = "PKG-0001",
                PackageFingerprint = "package-fingerprint",
                AuthorityResolvedAt = now
            }
            : null;
        bool diverged = includeSnapshot && !string.Equals(selectedOptionId, recommendedOptionId, StringComparison.Ordinal);
        DecisionResolution resolution = new(
            outcome,
            selectedOptionId,
            "Human resolution.",
            "reviewer",
            diverged,
            now,
            [DecisionRecordSource(id)],
            snapshot);

        return new Decision(
            id,
            outcome == DecisionOutcome.Accepted ? DecisionState.Resolved : DecisionState.Archived,
            DecisionClassification.Architectural,
            "Choose decision quality path",
            "Decide how to assess generated decisions.",
            new DecisionMetadata(repositoryId, now, now),
            resolution,
            [],
            [],
            [new DecisionHistoryEntry(now, "Resolved", DecisionState.Open.ToString(), DecisionState.Resolved.ToString(), "Seeded by quality test.", [])]);
    }

    private static DecisionProposal CreateProposal(Guid repositoryId)
    {
        return new DecisionProposal(
            "PROP-0001",
            repositoryId,
            "CAND-0001",
            DecisionProposalState.Resolved,
            "Choose decision quality path",
            "Decide how to assess generated decisions.",
            Options(),
            [],
            new DecisionRecommendation("option-1", "Recommended from generated evidence.", []),
            [],
            [],
            []);
    }

    private static DecisionPackageVersion CreatePackageVersion(Guid repositoryId)
    {
        DecisionCandidate candidate = new(
            "CAND-0001",
            repositoryId,
            DecisionCandidateState.Promoted,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Choose decision quality path",
            "Decide how to assess generated decisions.",
            "source-fingerprint",
            [],
            [],
            [],
            [],
            []);
        DecisionPackage package = new(
            "PKG-0001",
            repositoryId,
            "PROP-0001",
            "CAND-0001",
            "Choose decision quality path",
            "Decide how to assess generated decisions.",
            candidate,
            DecisionGenerationContext.Empty(repositoryId),
            Options(),
            [],
            [],
            [],
            [],
            new DecisionRecommendation("option-1", "Recommended from generated evidence.", []),
            [],
            [],
            [],
            new DecisionPackageMetadata(
                "context-fingerprint",
                "test-generator",
                "CAND-0001",
                "repository-fingerprint",
                "M8",
                ".agents/milestones/m8-decision-quality.md",
                "PROP-0001",
                "proposal-fingerprint"),
            null,
            null,
            DateTimeOffset.UtcNow);
        return new DecisionPackageVersion("PKG-0001", repositoryId, "PROP-0001", "CAND-0001", DateTimeOffset.UtcNow, "package-fingerprint", package);
    }

    private static IReadOnlyList<DecisionOption> Options()
    {
        return
        [
            new DecisionOption("option-1", "Assess from resolution evidence", "Use generated decision lifecycle evidence.", []),
            new DecisionOption("option-2", "Assess from alternative usage", "Measure whether alternatives were selected.", [])
        ];
    }

    private static DecisionSourceReference DecisionRecordSource(DecisionId decisionId)
    {
        return new DecisionSourceReference(
            "DecisionRecord",
            $".agents/decisions/records/{decisionId.Value}/decision.json",
            DecisionId: decisionId);
    }

    private static DecisionSourceReference ProposalSource(string proposalId, DecisionId decisionId)
    {
        return new DecisionSourceReference(
            "DecisionProposalRevision",
            $".agents/decisions/proposals/{proposalId}/revisions/REV-0001.json",
            ItemId: "REV-0001",
            DecisionId: decisionId,
            ProposalId: proposalId);
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
