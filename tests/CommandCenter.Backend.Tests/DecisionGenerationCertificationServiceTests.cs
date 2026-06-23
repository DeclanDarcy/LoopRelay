using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionGenerationCertificationServiceTests
{
    [Fact]
    public async Task CertificationPassesForGeneratedResolvedDecisionWithQualityAndInfluence()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync();

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.RunCertificationAsync(harness.Repository.Id);
        IReadOnlyList<DecisionGenerationCertificationReport> reloaded =
            await harness.CertificationService.ListReportsAsync(harness.Repository.Id);

        Assert.True(report.Result.Certified);
        Assert.True(report.Result.GenerationCertified);
        Assert.True(report.Result.GovernanceCertified);
        Assert.True(report.Result.QualityCertified);
        Assert.True(report.Result.ConsumptionCertified);
        Assert.True(report.Result.WorkflowReplacementCertified);
        Assert.Empty(report.Result.Failures);
        Assert.Equal(1, report.GeneratedResolvedDecisionCount);
        Assert.Equal(1, report.ExecutionInfluenceTraceCount);
        Assert.Equal(1, report.HumanAuthoringBurden.ReviewOnlyCount);
        Assert.Single(report.QualityAssessments);
        DecisionGenerationCertificationReport persisted = Assert.Single(reloaded);
        Assert.Equal(report.Id, persisted.Id);
        DecisionCandidate candidate = Assert.Single(await harness.DecisionRepository.ListCandidatesAsync(harness.Repository));
        Assert.Contains(candidate.History, entry => entry.Event == "Discovered");
        Assert.Contains(candidate.History, entry => entry.Event == "Promoted");
        Assert.True(File.Exists(Path.Combine(
            harness.Repository.Path,
            ".agents",
            "decisions",
            "certification",
            $"{report.Id}.json")));
    }

    [Fact]
    public async Task CertificationFailsWhenGeneratedDecisionHasNoInfluenceTrace()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(recordInfluence: false);

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.True(report.Result.GenerationCertified);
        Assert.True(report.Result.GovernanceCertified);
        Assert.True(report.Result.QualityCertified);
        Assert.False(report.Result.ConsumptionCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("CON-002:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenExecutionProjectionIsAbsentEvenWithInfluenceTrace()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync();
        var certificationService = new DecisionGenerationCertificationService(
            harness.RepositoryService,
            harness.DecisionRepository,
            new EmptyProjectionService(harness.Repository.Id),
            harness.InfluenceService,
            harness.BurdenService);

        DecisionGenerationCertificationReport report =
            await certificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.True(report.Result.GenerationCertified);
        Assert.True(report.Result.GovernanceCertified);
        Assert.True(report.Result.QualityCertified);
        Assert.False(report.Result.ConsumptionCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("CON-001:", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Result.Failures, failure => failure.StartsWith("CON-002:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenRecommendationIsOrderBasedInsteadOfTopEvaluatedOption()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(proposal =>
        {
            DecisionRecommendation recommendation = proposal.Recommendation!;
            OptionEvaluation[] evaluations = recommendation.OptionEvaluations
                .Select(evaluation => evaluation.OptionId == recommendation.OptionId
                    ? evaluation with { Score = 0, Rank = 2 }
                    : evaluation with { Score = 100, Rank = 1 })
                .ToArray();
            return proposal with
            {
                Recommendation = recommendation with { OptionEvaluations = evaluations }
            };
        });

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.False(report.Result.GenerationCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("GEN-006:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenResolvedGeneratedDecisionHasOnlyOneOption()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(proposal =>
        {
            DecisionOption selected = proposal.Options[0];
            return proposal with
            {
                Options = [selected],
                Tradeoffs = proposal.Tradeoffs
                    .Where(tradeoff => tradeoff.OptionId == selected.Id)
                    .ToArray(),
                Recommendation = proposal.Recommendation! with
                {
                    OptionEvaluations = proposal.Recommendation.OptionEvaluations
                        .Where(evaluation => evaluation.OptionId == selected.Id)
                        .ToArray(),
                    RecommendationEvidence = proposal.Recommendation.RecommendationEvidence
                        .Where(evidence => evidence.OptionId == selected.Id)
                        .ToArray()
                }
            };
        });

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.False(report.Result.GenerationCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("GEN-002:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenRecommendationLacksEvidence()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(proposal =>
            proposal with
            {
                Recommendation = proposal.Recommendation! with
                {
                    Evidence = [],
                    RecommendationEvidence = []
                }
            });

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.False(report.Result.GenerationCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("GEN-006:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenRecommendationIsMissing()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(proposal =>
            proposal with { Recommendation = null });

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.False(report.Result.GenerationCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("GEN-004:", StringComparison.Ordinal));
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("GEN-006:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenResolvedGeneratedDecisionLacksTradeoffCoverage()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(proposal =>
            proposal with { Tradeoffs = proposal.Tradeoffs.Take(1).ToArray() });

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.False(report.Result.GenerationCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("GEN-003:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenResolvedGeneratedDecisionHasNoQualityAssessment()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(saveQualityAssessment: false);

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.False(report.Result.QualityCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("QLT-001:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenGeneratedDecisionRequiresFullRewrite()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(
            revisionBurden: HumanAuthoringBurden.FullRewrite);

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.False(report.Result.WorkflowReplacementCertified);
        Assert.Equal(1, report.HumanAuthoringBurden.FullRewriteCount);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("BUR-001:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationTracksRepeatedIgnoredRecommendationsWithoutFailingCertification()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(selectAlternativeOption: true);
        await AddRepeatedRecommendationDivergenceAsync(harness);

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.True(report.Result.Certified);
        DecisionGenerationCertificationFinding finding = Assert.Single(
            report.Result.Findings,
            finding => finding.Id == "QLT-002");
        Assert.True(finding.Passed);
        Assert.Contains("advisory quality signals", finding.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(report.Result.Failures, failure => failure.StartsWith("QLT-002:", StringComparison.Ordinal));
        Assert.Contains(
            report.QualityAssessments.SelectMany(assessment => assessment.Signals),
            signal => signal.Category == "RecommendationStability" &&
                signal.Direction == QualitySignalDirection.Negative);
    }

    [Fact]
    public async Task CertificationFailsWhenManualDecisionBypassesGeneration()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await harness.DecisionRepository.SaveDecisionAsync(
            harness.Repository,
            new Decision(
                new DecisionId("DEC-9999"),
                DecisionState.Resolved,
                DecisionClassification.Tactical,
                "Manual bypass decision",
                "Manual decision should count as generation bypass evidence.",
                new DecisionMetadata(harness.Repository.Id, now, now),
                new DecisionResolution(
                    DecisionOutcome.Accepted,
                    "manual-option",
                    "Resolved outside generated proposal flow.",
                    "human-reviewer",
                    false,
                    now,
                    [new DecisionSourceReference("ManualDecision", ".agents/decisions/records/DEC-9999/decision.json")]),
                [],
                [],
                []));

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.False(report.Result.WorkflowReplacementCertified);
        Assert.Equal(1, report.HumanAuthoringBurden.GenerationBypassedCount);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("BUR-001:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CertificationFailsWhenGeneratedDecisionClaimsSystemResolutionAuthority()
    {
        CertificationHarness harness = await CreateGeneratedDecisionHarnessAsync(resolver: "system");

        DecisionGenerationCertificationReport report =
            await harness.CertificationService.GetCurrentCertificationAsync(harness.Repository.Id);

        Assert.False(report.Result.Certified);
        Assert.False(report.Result.GovernanceCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("GOV-001:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerationCertificationEndpointsReturnCurrentReportPersistedRunAndHistory()
    {
        Repository repository = CreateRepository();

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        HttpResponseMessage currentResponse =
            await client.GetAsync($"{root}/api/repositories/{repository.Id}/decisions/generation-certification/current");
        HttpResponseMessage runResponse =
            await client.PostAsync($"{root}/api/repositories/{repository.Id}/decisions/generation-certification", null);
        HttpResponseMessage historyResponse =
            await client.GetAsync($"{root}/api/repositories/{repository.Id}/decisions/generation-certification/reports");

        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        DecisionGenerationCertificationReport? current =
            await currentResponse.Content.ReadFromJsonAsync<DecisionGenerationCertificationReport>(jsonOptions);
        DecisionGenerationCertificationReport? persisted =
            await runResponse.Content.ReadFromJsonAsync<DecisionGenerationCertificationReport>(jsonOptions);
        DecisionGenerationCertificationReport[]? history =
            await historyResponse.Content.ReadFromJsonAsync<DecisionGenerationCertificationReport[]>(jsonOptions);
        Assert.NotNull(current);
        Assert.NotNull(persisted);
        Assert.NotNull(history);
        Assert.StartsWith("generation-certification.", current.Id, StringComparison.Ordinal);
        Assert.StartsWith("generation-certification.", persisted.Id, StringComparison.Ordinal);
        Assert.False(current.Result.Certified);
        Assert.False(persisted.Result.Certified);
        Assert.Single(history);
        Assert.Equal(persisted.Id, history[0].Id);
    }

    private static DecisionCandidate CreateCandidate(
        Guid repositoryId,
        DecisionCandidateState state)
    {
        DecisionEvidence evidence = new(
            "Plan requires automated generation certification.",
            [new DecisionSourceReference(
                "Plan",
                ".agents/plan.md",
                Section: "Milestone 10",
                ItemId: "m10",
                Excerpt: "Certify generated decisions reach execution through human governance.")]);
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            state,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Certify generated decision workflow",
            "Need to certify generated decisions reach execution influence.",
            "source-fingerprint",
            [new DecisionSignal(
                "WorkflowContinuation",
                "Need to certify generated decisions reach execution influence.",
                DecisionClassification.Architectural,
                DecisionCandidatePriority.High,
                [evidence])],
            [evidence],
            [new DecisionSourceReference(
                "Plan",
                ".agents/plan.md",
                Section: "Milestone 10",
                ItemId: "m10",
                Excerpt: "Certify generated decisions reach execution through human governance.")],
            ["Created by generation certification test."],
            [new DecisionHistoryEntry(
                DateTimeOffset.UtcNow,
                "Promoted",
                null,
                state.ToString(),
                "Seeded by generation certification test.",
                [])]);
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

    private static async Task<CertificationHarness> CreateGeneratedDecisionHarnessAsync(
        Func<DecisionProposal, DecisionProposal>? mutateProposal = null,
        bool saveQualityAssessment = true,
        bool recordInfluence = true,
        HumanAuthoringBurden? revisionBurden = null,
        string resolver = "human-reviewer",
        bool selectAlternativeOption = false)
    {
        Repository repository = CreateRepository();
        await WriteAsync(
            repository,
            ".agents/plan.md",
            """
            # Plan

            - Need to decide architectural execution projection strategy for generated decisions.
            """);
        await WriteAsync(
            repository,
            ".agents/milestones/m10-generation-certification.md",
            "# M10\n\n- Certify generated decisions reach execution influence.");
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        var contextService = new DecisionContextService(repositoryService, store, decisionRepository);
        var discoveryService = new DecisionDiscoveryService(
            repositoryService,
            contextService,
            decisionRepository,
            projectionService);
        DecisionCandidate discovered = Assert.Single((await discoveryService.DiscoverAsync(repository.Id)).Candidates);
        DecisionCandidate candidate = await discoveryService.PromoteCandidateAsync(
            repository.Id,
            discovered.Id,
            "Ready for proposal generation.");
        var generationService = new DecisionGenerationService(
            repositoryService,
            decisionRepository,
            projectionService,
            new OptionGenerationService(),
            contextService);
        var resolutionService = new DecisionResolutionService(repositoryService, decisionRepository, projectionService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        if (mutateProposal is not null)
        {
            proposal = mutateProposal(proposal);
            await decisionRepository.SaveProposalAsync(repository, proposal);
            await SaveMatchingPackageVersionAsync(repository, decisionRepository, proposal);
        }

        await generationService.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for human governance.");
        if (revisionBurden is not null)
        {
            await decisionRepository.SaveProposalRevisionAsync(
                repository,
                new DecisionProposalRevision(
                    "REV-9999",
                    repository.Id,
                    proposal.Id,
                    DateTimeOffset.UtcNow,
                    "Seed certification burden fixture.",
                    ["Options", "Tradeoffs", "Recommendation"],
                    "fixture-fingerprint",
                    [new DecisionSourceReference(
                        "DecisionProposal",
                        $".agents/decisions/proposals/{proposal.Id}/proposal.json",
                        ProposalId: proposal.Id,
                        CandidateId: proposal.CandidateId)],
                    RequestedBy: "human-reviewer",
                    HumanAuthoringBurden: revisionBurden.Value));
        }

        string selectedOptionId = selectAlternativeOption
            ? proposal.Options.First(option =>
                !string.Equals(option.Id, proposal.Recommendation?.OptionId, StringComparison.Ordinal)).Id
            : string.IsNullOrWhiteSpace(proposal.Recommendation?.OptionId)
                ? proposal.Options[0].Id
                : proposal.Recommendation.OptionId;
        Decision decision = await resolutionService.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand("Accept generated option after human review.", resolver, selectedOptionId));
        var burdenService = new HumanAuthoringBurdenService(repositoryService, decisionRepository);
        if (saveQualityAssessment)
        {
            var qualityService = new DecisionQualityAssessmentService(
                repositoryService,
                decisionRepository,
                new DecisionQualitySignalService(repositoryService, decisionRepository, burdenService),
                burdenService,
                projectionService);
            await qualityService.AssessAndSaveDecisionAsync(repository.Id, decision.Id.Value);
        }

        var executionProjectionService = new DecisionProjectionService(
            repositoryService,
            decisionRepository,
            new HealthyGovernanceService(repository.Id),
            store);
        ExecutionDecisionProjection executionProjection =
            await executionProjectionService.BuildExecutionProjectionAsync(repository.Id);
        var influenceService = new DecisionInfluenceService(repositoryService, store);
        if (recordInfluence)
        {
            await influenceService.RecordExecutionInfluenceAsync(repository.Id, Guid.NewGuid(), executionProjection);
        }

        var certificationService = new DecisionGenerationCertificationService(
            repositoryService,
            decisionRepository,
            executionProjectionService,
            influenceService,
            burdenService);
        return new CertificationHarness(
            repository,
            repositoryService,
            decisionRepository,
            certificationService,
            burdenService,
            influenceService);
    }

    private static async Task AddRepeatedRecommendationDivergenceAsync(CertificationHarness harness)
    {
        Decision firstDecision = (await harness.DecisionRepository.ListDecisionsAsync(harness.Repository))
            .Single();
        DecisionResolvedProposalSnapshot snapshot = firstDecision.Resolution!.SourceProposalSnapshot!;
        string selectedOptionId = snapshot.Options
            .First(option => !string.Equals(option.Id, snapshot.Recommendation?.OptionId, StringComparison.Ordinal))
            .Id;
        DecisionId decisionId = await harness.DecisionRepository.AllocateDecisionIdAsync(harness.Repository);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var decision = new Decision(
            decisionId,
            DecisionState.Resolved,
            firstDecision.Classification,
            $"{firstDecision.Title} follow-up",
            firstDecision.Context,
            new DecisionMetadata(harness.Repository.Id, now, now),
            new DecisionResolution(
                DecisionOutcome.Accepted,
                selectedOptionId,
                "Accept generated alternative again after human review.",
                "human-reviewer",
                true,
                now,
                [
                    new DecisionSourceReference(
                        "DecisionProposal",
                        $".agents/decisions/proposals/{snapshot.ProposalId}/proposal.json",
                        DecisionId: decisionId,
                        ProposalId: snapshot.ProposalId,
                        CandidateId: snapshot.CandidateId),
                    new DecisionSourceReference(
                        "DecisionOption",
                        $".agents/decisions/proposals/{snapshot.ProposalId}/proposal.json",
                        Section: "Options",
                        ItemId: selectedOptionId,
                        DecisionId: decisionId,
                        ProposalId: snapshot.ProposalId,
                        CandidateId: snapshot.CandidateId)
                ],
                snapshot with { AuthorityResolvedAt = now }),
            [],
            firstDecision.Evidence,
            [
                new DecisionHistoryEntry(
                    now,
                    "Resolved",
                    DecisionState.Open.ToString(),
                    DecisionState.Resolved.ToString(),
                    "Accept generated alternative again after human review.",
                    [])
            ]);
        await harness.DecisionRepository.SaveDecisionAsync(harness.Repository, decision);

        var projectionService = new DecisionArtifactProjectionService(
            harness.DecisionRepository,
            new FileSystemArtifactStore());
        var qualityService = new DecisionQualityAssessmentService(
            harness.RepositoryService,
            harness.DecisionRepository,
            new DecisionQualitySignalService(harness.RepositoryService, harness.DecisionRepository, harness.BurdenService),
            harness.BurdenService,
            projectionService);
        await qualityService.AssessAndSaveDecisionAsync(harness.Repository.Id, decision.Id.Value);

        var executionProjectionService = new DecisionProjectionService(
            harness.RepositoryService,
            harness.DecisionRepository,
            new HealthyGovernanceService(harness.Repository.Id),
            new FileSystemArtifactStore());
        ExecutionDecisionProjection executionProjection =
            await executionProjectionService.BuildExecutionProjectionAsync(harness.Repository.Id);
        await harness.InfluenceService.RecordExecutionInfluenceAsync(harness.Repository.Id, Guid.NewGuid(), executionProjection);
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static async Task SaveMatchingPackageVersionAsync(
        Repository repository,
        IDecisionRepository decisionRepository,
        DecisionProposal proposal)
    {
        DecisionPackageVersion latestPackage = (await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id))
            .OrderBy(version => version.CreatedAt)
            .ThenBy(version => version.Id, StringComparer.Ordinal)
            .Last();
        const string packageId = "PKG-9999";
        DecisionPackage package = latestPackage.Package with
        {
            Id = packageId,
            Title = proposal.Title,
            DecisionSummary = proposal.Context,
            Options = proposal.Options,
            OptionRelationships = proposal.OptionRelationships,
            AnalyzedOptions = proposal.AnalyzedOptions,
            Tradeoffs = proposal.Tradeoffs,
            TradeoffComparisons = proposal.TradeoffComparisons,
            Recommendation = proposal.Recommendation,
            Assumptions = proposal.Assumptions,
            Evidence = proposal.Evidence,
            OpenConcerns = proposal.Recommendation?.Concerns ?? [],
            GenerationDiagnostics = proposal.GenerationDiagnostics,
            TradeoffAnalysisDiagnostics = proposal.TradeoffAnalysisDiagnostics
        };
        await decisionRepository.SavePackageVersionAsync(
            repository,
            new DecisionPackageVersion(
                packageId,
                repository.Id,
                proposal.Id,
                proposal.CandidateId,
                DateTimeOffset.UtcNow.AddSeconds(1),
                "mutated-package-fingerprint",
                package));
    }

    private sealed record CertificationHarness(
        Repository Repository,
        IRepositoryService RepositoryService,
        IDecisionRepository DecisionRepository,
        DecisionGenerationCertificationService CertificationService,
        IHumanAuthoringBurdenService BurdenService,
        IDecisionInfluenceService InfluenceService);

    private sealed class EmptyProjectionService(Guid repositoryId) : IDecisionProjectionService
    {
        public Task<ExecutionDecisionProjection> BuildExecutionProjectionAsync(
            Guid requestedRepositoryId,
            string? executionRequest = null,
            string? milestoneContent = null)
        {
            Assert.Equal(repositoryId, requestedRepositoryId);
            var context = new ExecutionDecisionContext([], [], [], [], [], ["Projection was absent in certification fixture."]);
            return Task.FromResult(new ExecutionDecisionProjection(
                repositoryId,
                DateTimeOffset.UtcNow,
                [],
                [],
                [],
                [],
                [],
                ["Projection was absent in certification fixture."],
                context,
                "empty-projection-fixture"));
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
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

    private sealed class HealthyGovernanceService(Guid repositoryId) : IDecisionGovernanceService
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
                DecisionHealthAssessment.Healthy,
                new DecisionGovernanceSummary(0, 0, 0, 0, 0, 0, 0),
                [],
                []);
        }
    }
}
