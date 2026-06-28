using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Backend;
using CommandCenter.Backend.Services;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommandCenter.Backend.Tests;

[Collection("ProcessEnvironment")]
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
        Assert.NotNull(assessment.QualityExplanation);
        Assert.Contains(assessment.QualityExplanation.SignalContributions, contribution =>
            contribution.Category == "RecommendationQuality" &&
            contribution.ScoreContribution > 0);
        Assert.Equal(50, assessment.QualityExplanation.BaseScore);
        Assert.Contains("threshold", assessment.QualityExplanation.Threshold.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(assessment.HumanAuthoringBurdenSignals, signal =>
            signal.Burden == HumanAuthoringBurden.ReviewOnly);
        Assert.NotNull(assessment.HumanAuthoringBurdenExplanation);
        Assert.Equal(HumanAuthoringBurden.ReviewOnly, assessment.HumanAuthoringBurdenExplanation.EffectiveBurden);
        Assert.False(assessment.HumanAuthoringBurdenExplanation.IsUnknown);
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
    public async Task RepeatedRecommendationReversalReducesStability()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Decision previous = CreateDecision(
            repository.Id,
            DecisionOutcome.Accepted,
            "option-2",
            recommendedOptionId: "option-1",
            resolvedAt: now.AddMinutes(-10));
        Decision current = CreateDecision(
            repository.Id,
            DecisionOutcome.Accepted,
            "option-2",
            recommendedOptionId: "option-1",
            decisionId: "DEC-0002",
            resolvedAt: now);
        await decisionRepository.SaveDecisionAsync(repository, previous);
        await decisionRepository.SaveDecisionAsync(repository, current);
        IDecisionQualityAssessmentService service = CreateAssessmentService(repository, decisionRepository);

        DecisionQualityAssessment assessment = await service.AssessDecisionAsync(repository.Id, current.Id.Value);

        Assert.Contains(assessment.Signals, signal =>
            signal.Category == "RecommendationStability" &&
            signal.Direction == QualitySignalDirection.Negative &&
            signal.Severity == QualitySignalSeverity.High &&
            signal.Detail.Contains("2 of 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task QualitySignalsEvaluateTradeoffContextAndConstraintCategories()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateDecision(
            repository.Id,
            DecisionOutcome.Accepted,
            "option-2",
            recommendedOptionId: "option-1",
            disqualifyingConstraints: ["option-2 violates a preserved compatibility constraint."]);
        await decisionRepository.SaveDecisionAsync(repository, decision);
        IDecisionQualityAssessmentService service = CreateAssessmentService(repository, decisionRepository);

        DecisionQualityAssessment assessment = await service.AssessDecisionAsync(repository.Id, decision.Id.Value);

        Assert.Contains(assessment.Signals, signal =>
            signal.Category == "TradeoffQuality" &&
            signal.Direction == QualitySignalDirection.Positive);
        Assert.Contains(assessment.Signals, signal =>
            signal.Category == "ContextQuality" &&
            signal.Direction == QualitySignalDirection.Positive);
        Assert.Contains(assessment.Signals, signal =>
            signal.Category == "ConstraintQuality" &&
            signal.Direction == QualitySignalDirection.Negative &&
            signal.Severity == QualitySignalSeverity.High);
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
        Assert.Equal(HumanAuthoringBurden.FullRewrite, rewrittenAssessment.HumanAuthoringBurdenExplanation?.EffectiveBurden);
        Assert.Contains(bypassedAssessment.HumanAuthoringBurdenSignals, signal =>
            signal.Burden == HumanAuthoringBurden.GenerationBypassed);
        Assert.Contains(bypassedAssessment.Signals, signal =>
            signal.Category == "HumanAuthoringBurden" &&
            signal.Severity == QualitySignalSeverity.Critical);
        Assert.Contains(report.HumanAuthoringBurdenExplanations ?? [], explanation =>
            explanation.DecisionId == rewritten.Id.Value &&
            explanation.EffectiveBurden == HumanAuthoringBurden.FullRewrite &&
            explanation.WinningSignal?.Burden == HumanAuthoringBurden.FullRewrite);
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

    [Fact]
    public async Task FileSystemRepositoryPersistsQualityArtifactsAndMarkdownProjections()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        Decision decision = CreateDecision(repository.Id, DecisionOutcome.Accepted, "option-1", recommendedOptionId: "option-1");
        await decisionRepository.SaveDecisionAsync(repository, decision);
        IDecisionQualityAssessmentService assessmentService = CreateAssessmentService(repository, decisionRepository);
        IDecisionQualityReportService reportService = CreateReportService(repository, decisionRepository, assessmentService);
        DecisionQualityAssessment assessment = await assessmentService.AssessDecisionAsync(repository.Id, decision.Id.Value);
        DecisionQualityReport report = await reportService.GenerateReportAsync(repository.Id);
        DecisionQualityTrend trend = reportService.GenerateTrend(repository.Id, [], [assessment]);

        await decisionRepository.SaveQualityAssessmentAsync(repository, assessment);
        await decisionRepository.SaveQualityReportAsync(repository, report);
        await decisionRepository.SaveQualityTrendAsync(repository, trend);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        await projectionService.ProjectQualityAssessmentAsync(repository, assessment);
        await projectionService.ProjectQualityReportAsync(repository, report);
        await projectionService.ProjectQualityTrendAsync(repository, trend);

        var restartedRepository = new FileSystemDecisionRepository(store);
        DecisionQualityAssessment reloadedAssessment = Assert.Single(await restartedRepository.ListQualityAssessmentsAsync(repository));
        DecisionQualityReport reloadedReport = Assert.Single(await restartedRepository.ListQualityReportsAsync(repository));
        DecisionQualityTrend reloadedTrend = Assert.Single(await restartedRepository.ListQualityTrendsAsync(repository));

        Assert.Equal(assessment.Id, reloadedAssessment.Id);
        Assert.Equal(report.Id, reloadedReport.Id);
        Assert.Equal(trend.Id, reloadedTrend.Id);
        Assert.Equal(decision.Id.Value, reloadedAssessment.DecisionId);
        Assert.NotNull(reloadedAssessment.QualityExplanation);
        Assert.NotNull(reloadedAssessment.HumanAuthoringBurdenExplanation);
        Assert.Contains(reloadedReport.Assessments, item => item.DecisionId == decision.Id.Value);
        Assert.Contains(reloadedReport.HumanAuthoringBurdenExplanations ?? [], explanation =>
            explanation.DecisionId == decision.Id.Value &&
            explanation.EffectiveBurden == HumanAuthoringBurden.ReviewOnly);
        Assert.True(File.Exists(PathFor(repository, $".agents/decisions/quality/assessments/{assessment.Id}.json")));
        Assert.True(File.Exists(PathFor(repository, $".agents/decisions/quality/assessments/{assessment.Id}.md")));
        Assert.True(File.Exists(PathFor(repository, $".agents/decisions/quality/reports/{report.Id}.json")));
        Assert.True(File.Exists(PathFor(repository, $".agents/decisions/quality/reports/{report.Id}.md")));
        Assert.True(File.Exists(PathFor(repository, $".agents/decisions/quality/trends/{trend.Id}.json")));
        Assert.True(File.Exists(PathFor(repository, $".agents/decisions/quality/trends/{trend.Id}.md")));
        Assert.Contains("# " + assessment.Id + ": Decision Quality Assessment", await File.ReadAllTextAsync(PathFor(repository, $".agents/decisions/quality/assessments/{assessment.Id}.md")));
        Assert.Contains("## Quality Explanation", await File.ReadAllTextAsync(PathFor(repository, $".agents/decisions/quality/assessments/{assessment.Id}.md")));
        Assert.Contains("## Human Authoring Burden Explanation", await File.ReadAllTextAsync(PathFor(repository, $".agents/decisions/quality/assessments/{assessment.Id}.md")));
        Assert.Contains("## Human Authoring Burden", await File.ReadAllTextAsync(PathFor(repository, $".agents/decisions/quality/reports/{report.Id}.md")));
        Assert.Contains("### Effective Burden By Decision", await File.ReadAllTextAsync(PathFor(repository, $".agents/decisions/quality/reports/{report.Id}.md")));
        Assert.Contains("- Direction: Positive", await File.ReadAllTextAsync(PathFor(repository, $".agents/decisions/quality/trends/{trend.Id}.md")));
    }

    [Fact]
    public async Task QualityServicesSaveAndListPersistedHistory()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateDecision(repository.Id, DecisionOutcome.Accepted, "option-1", recommendedOptionId: "option-1");
        await decisionRepository.SaveDecisionAsync(repository, decision);
        IDecisionQualityAssessmentService assessmentService = CreateAssessmentService(repository, decisionRepository);
        IDecisionQualityReportService reportService = CreateReportService(repository, decisionRepository, assessmentService);

        DecisionQualityAssessment assessment =
            await assessmentService.AssessAndSaveDecisionAsync(repository.Id, decision.Id.Value);
        DecisionQualityReport report = await reportService.GenerateAndSaveReportAsync(repository.Id);
        DecisionQualityTrend trend = await reportService.GenerateAndSaveTrendFromHistoryAsync(repository.Id);

        DecisionQualityAssessment persistedAssessment =
            Assert.Single(await assessmentService.ListAssessmentsAsync(repository.Id));
        DecisionQualityReport persistedReport = Assert.Single(await reportService.ListReportsAsync(repository.Id));
        DecisionQualityTrend persistedTrend = Assert.Single(await reportService.ListTrendsAsync(repository.Id));
        Assert.Equal(assessment.Id, persistedAssessment.Id);
        Assert.Equal(report.Id, persistedReport.Id);
        Assert.Equal(trend.Id, persistedTrend.Id);
        Assert.Contains(report.Assessments, item => item.DecisionId == decision.Id.Value);
        Assert.Contains(trend.Diagnostics, diagnostic =>
            diagnostic.Contains("persisted assessment history", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task QualityTrendUsesPersistedAssessmentHistory()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        Decision decision = CreateDecision(repository.Id, DecisionOutcome.Accepted, "option-1", recommendedOptionId: "option-1");
        await decisionRepository.SaveDecisionAsync(repository, decision);
        IDecisionQualityAssessmentService assessmentService = CreateAssessmentService(repository, decisionRepository);
        IDecisionQualityReportService reportService = CreateReportService(repository, decisionRepository, assessmentService);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DecisionQualityAssessment previous = CreateAssessment(repository.Id, decision.Id.Value, "assessment.202606230101010000000", now.AddMinutes(-5), 40);
        DecisionQualityAssessment current = CreateAssessment(repository.Id, decision.Id.Value, "assessment.202606230102010000000", now, 80);
        await assessmentService.SaveAssessmentAsync(repository.Id, previous);
        await assessmentService.SaveAssessmentAsync(repository.Id, current);

        DecisionQualityTrend trend = await reportService.GenerateTrendFromHistoryAsync(repository.Id);

        Assert.Equal(1, trend.AssessmentCount);
        Assert.Equal(80, trend.CurrentAverageScore);
        Assert.Equal(40, trend.PreviousAverageScore);
        Assert.Equal(QualitySignalDirection.Positive, trend.Direction);
        Assert.Contains(trend.Diagnostics, diagnostic =>
            diagnostic.Contains("Compared 1 previous assessment(s) with 1 current assessment(s).", StringComparison.Ordinal));
    }

    [Fact]
    public async Task QualityEndpointsExposeAssessmentReportTrendAndPersistedHistory()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        DecisionProposal proposal = CreateProposal(repository.Id);
        Decision decision = CreateDecision(repository.Id, DecisionOutcome.Accepted, "option-1", recommendedOptionId: "option-1");
        await decisionRepository.SaveProposalAsync(repository, proposal);
        await decisionRepository.SaveDecisionAsync(repository, decision);

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        HttpResponseMessage assessmentResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/quality/assess",
            null);
        HttpResponseMessage assessmentsResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/quality/assessments");
        HttpResponseMessage currentReportResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/quality/reports/current");
        HttpResponseMessage reportResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/quality/reports",
            null);
        HttpResponseMessage reportsResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/quality/reports");
        HttpResponseMessage currentTrendResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/quality/trends/current");
        HttpResponseMessage trendResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/quality/trends",
            null);
        HttpResponseMessage trendsResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/quality/trends");

        Assert.Equal(HttpStatusCode.OK, assessmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assessmentsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentReportResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reportsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentTrendResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, trendResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, trendsResponse.StatusCode);

        DecisionQualityAssessment assessment =
            (await assessmentResponse.Content.ReadFromJsonAsync<DecisionQualityAssessment>(jsonOptions))!;
        DecisionQualityAssessment[] assessments =
            (await assessmentsResponse.Content.ReadFromJsonAsync<DecisionQualityAssessment[]>(jsonOptions))!;
        DecisionQualityReport currentReport =
            (await currentReportResponse.Content.ReadFromJsonAsync<DecisionQualityReport>(jsonOptions))!;
        DecisionQualityReport report =
            (await reportResponse.Content.ReadFromJsonAsync<DecisionQualityReport>(jsonOptions))!;
        DecisionQualityReport[] reports =
            (await reportsResponse.Content.ReadFromJsonAsync<DecisionQualityReport[]>(jsonOptions))!;
        DecisionQualityTrend currentTrend =
            (await currentTrendResponse.Content.ReadFromJsonAsync<DecisionQualityTrend>(jsonOptions))!;
        DecisionQualityTrend trend =
            (await trendResponse.Content.ReadFromJsonAsync<DecisionQualityTrend>(jsonOptions))!;
        DecisionQualityTrend[] trends =
            (await trendsResponse.Content.ReadFromJsonAsync<DecisionQualityTrend[]>(jsonOptions))!;

        Assert.Equal(decision.Id.Value, assessment.DecisionId);
        Assert.Contains(assessment.Signals, signal => signal.Category == "RecommendationQuality");
        Assert.NotNull(assessment.QualityExplanation);
        Assert.NotNull(assessment.HumanAuthoringBurdenExplanation);
        Assert.Single(assessments);
        Assert.Equal(assessment.Id, assessments[0].Id);
        Assert.Equal(1, currentReport.DecisionCount);
        Assert.Equal(1, report.GeneratedPackageCount);
        Assert.Contains(report.Assessments, item => item.DecisionId == decision.Id.Value);
        Assert.Contains(report.HumanAuthoringBurdenExplanations ?? [], explanation =>
            explanation.DecisionId == decision.Id.Value &&
            explanation.EffectiveBurden == HumanAuthoringBurden.ReviewOnly);
        Assert.Single(reports);
        Assert.Equal(report.Id, reports[0].Id);
        Assert.Equal(1, currentTrend.AssessmentCount);
        Assert.Single(trends);
        Assert.Equal(trend.Id, trends[0].Id);
        Assert.True(File.Exists(PathFor(repository, $".agents/decisions/quality/assessments/{assessment.Id}.md")));
        Assert.True(File.Exists(PathFor(repository, $".agents/decisions/quality/reports/{report.Id}.md")));
        Assert.True(File.Exists(PathFor(repository, $".agents/decisions/quality/trends/{trend.Id}.md")));
    }

    [Fact]
    public async Task QualityEndpointsReturnExpectedErrorStatusCodes()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        await decisionRepository.SaveProposalAsync(repository, CreateProposal(repository.Id));

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();

        HttpResponseMessage missingRepositoryResponse = await client.GetAsync(
            $"{root}/api/repositories/{Guid.NewGuid()}/decisions/quality/assessments");
        HttpResponseMessage missingProposalResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/PROP-9999/quality/assess",
            null);
        HttpResponseMessage invalidProposalResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/%20/quality/assess",
            null);
        HttpResponseMessage unresolvedProposalResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/PROP-0001/quality/assess",
            null);

        Assert.Equal(HttpStatusCode.NotFound, missingRepositoryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingProposalResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidProposalResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, unresolvedProposalResponse.StatusCode);
    }

    private static IDecisionQualityAssessmentService CreateAssessmentService(
        Repository repository,
        IDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, new FileSystemArtifactStore());
        var burdenService = new HumanAuthoringBurdenService(repositoryService, decisionRepository);
        var signalService = new DecisionQualitySignalService(repositoryService, decisionRepository, burdenService);
        return new DecisionQualityAssessmentService(repositoryService, decisionRepository, signalService, burdenService, projectionService);
    }

    private static IDecisionQualityReportService CreateReportService(
        Repository repository,
        IDecisionRepository decisionRepository,
        IDecisionQualityAssessmentService assessmentService)
    {
        return new DecisionQualityReportService(
            new StubRepositoryService(repository),
            decisionRepository,
            assessmentService,
            new DecisionArtifactProjectionService(decisionRepository, new FileSystemArtifactStore()));
    }

    private static Decision CreateDecision(
        Guid repositoryId,
        DecisionOutcome outcome,
        string selectedOptionId,
        string recommendedOptionId,
        string decisionId = "DEC-0001",
        bool includeSnapshot = true,
        DateTimeOffset? resolvedAt = null,
        IReadOnlyList<string>? disqualifyingConstraints = null)
    {
        DateTimeOffset now = resolvedAt ?? DateTimeOffset.UtcNow;
        var id = new DecisionId(decisionId);
        DecisionEvidence[] evidence = [new("Generated from quality test evidence.", [DecisionRecordSource(id)])];
        DecisionTradeoff[] tradeoffs =
        [
            new("option-1", "Keeps recommendation quality measurable.", "May miss alternative utilization.", evidence),
            new("option-2", "Measures alternative utilization.", "May expose recommendation divergence.", evidence)
        ];
        DecisionTradeoffComparison[] comparisons = disqualifyingConstraints is { Count: > 0 }
            ?
            [
                new DecisionTradeoffComparison(
                    selectedOptionId,
                    [],
                    [],
                    [],
                    [],
                    disqualifyingConstraints,
                    evidence)
            ]
            : [];
        DecisionResolvedProposalSnapshot? snapshot = includeSnapshot
            ? new DecisionResolvedProposalSnapshot(
                "PROP-0001",
                "CAND-0001",
                "proposal-fingerprint",
                DecisionProposalState.ReadyForResolution,
                "Choose decision quality path",
                "Decide how to assess generated decisions.",
                Options(),
                tradeoffs,
                new DecisionRecommendation(
                    recommendedOptionId,
                    "Recommended from generated evidence.",
                    evidence)
                {
                    Summary = "Prefer generated path.",
                    OptionEvaluations = disqualifyingConstraints is { Count: > 0 }
                        ?
                        [
                            new OptionEvaluation(
                                selectedOptionId,
                                [],
                                [],
                                [],
                                disqualifyingConstraints,
                                "Selected option has a disqualifying constraint.",
                                -50,
                                2,
                                "Constraint penalty applied.",
                                [])
                        ]
                        : []
                },
                [],
                evidence,
                [],
                [])
            {
                PackageId = "PKG-0001",
                PackageFingerprint = "package-fingerprint",
                AuthorityResolvedAt = now,
                TradeoffComparisons = comparisons
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

    private static DecisionQualityAssessment CreateAssessment(
        Guid repositoryId,
        string decisionId,
        string assessmentId,
        DateTimeOffset assessedAt,
        int score)
    {
        return new DecisionQualityAssessment(
            assessmentId,
            repositoryId,
            decisionId,
            assessedAt,
            DecisionQualityRating.Mixed,
            score,
            [],
            [],
            ["Seeded persisted quality assessment."]);
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

    private static string PathFor(Repository repository, string relativePath)
    {
        return Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
}
