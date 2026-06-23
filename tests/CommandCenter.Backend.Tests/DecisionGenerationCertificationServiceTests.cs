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
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        var generationService = new DecisionGenerationService(
            repositoryService,
            decisionRepository,
            projectionService,
            new OptionGenerationService(),
            new DecisionContextService(repositoryService, store, decisionRepository));
        var resolutionService = new DecisionResolutionService(repositoryService, decisionRepository, projectionService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for human governance.");
        Decision decision = await resolutionService.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand("Accept generated option after human review.", "human-reviewer", "option-1"));
        var burdenService = new HumanAuthoringBurdenService(repositoryService, decisionRepository);
        var qualityService = new DecisionQualityAssessmentService(
            repositoryService,
            decisionRepository,
            new DecisionQualitySignalService(repositoryService, decisionRepository, burdenService),
            burdenService,
            projectionService);
        await qualityService.AssessAndSaveDecisionAsync(repository.Id, decision.Id.Value);
        var executionProjectionService = new DecisionProjectionService(
            repositoryService,
            decisionRepository,
            new HealthyGovernanceService(repository.Id),
            store);
        ExecutionDecisionProjection executionProjection =
            await executionProjectionService.BuildExecutionProjectionAsync(repository.Id);
        var influenceService = new DecisionInfluenceService(repositoryService, store);
        await influenceService.RecordExecutionInfluenceAsync(repository.Id, Guid.NewGuid(), executionProjection);
        var certificationService = new DecisionGenerationCertificationService(
            repositoryService,
            decisionRepository,
            executionProjectionService,
            influenceService,
            burdenService);

        DecisionGenerationCertificationReport report =
            await certificationService.RunCertificationAsync(repository.Id);
        IReadOnlyList<DecisionGenerationCertificationReport> reloaded =
            await certificationService.ListReportsAsync(repository.Id);

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
        Assert.True(File.Exists(Path.Combine(
            repository.Path,
            ".agents",
            "decisions",
            "certification",
            $"{report.Id}.json")));
    }

    [Fact]
    public async Task CertificationFailsWhenGeneratedDecisionHasNoInfluenceTrace()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        var generationService = new DecisionGenerationService(
            repositoryService,
            decisionRepository,
            projectionService,
            new OptionGenerationService(),
            new DecisionContextService(repositoryService, store, decisionRepository));
        var resolutionService = new DecisionResolutionService(repositoryService, decisionRepository, projectionService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await generationService.MarkProposalReadyForResolutionAsync(repository.Id, proposal.Id, "Ready for human governance.");
        Decision decision = await resolutionService.ResolveProposalAsync(
            repository.Id,
            proposal.Id,
            new ResolveDecisionCommand("Accept generated option after human review.", "human-reviewer", "option-1"));
        var burdenService = new HumanAuthoringBurdenService(repositoryService, decisionRepository);
        var qualityService = new DecisionQualityAssessmentService(
            repositoryService,
            decisionRepository,
            new DecisionQualitySignalService(repositoryService, decisionRepository, burdenService),
            burdenService,
            projectionService);
        await qualityService.AssessAndSaveDecisionAsync(repository.Id, decision.Id.Value);
        var executionProjectionService = new DecisionProjectionService(
            repositoryService,
            decisionRepository,
            new HealthyGovernanceService(repository.Id),
            store);
        var certificationService = new DecisionGenerationCertificationService(
            repositoryService,
            decisionRepository,
            executionProjectionService,
            new DecisionInfluenceService(repositoryService, store),
            burdenService);

        DecisionGenerationCertificationReport report =
            await certificationService.GetCurrentCertificationAsync(repository.Id);

        Assert.False(report.Result.Certified);
        Assert.True(report.Result.GenerationCertified);
        Assert.True(report.Result.GovernanceCertified);
        Assert.True(report.Result.QualityCertified);
        Assert.False(report.Result.ConsumptionCertified);
        Assert.Contains(report.Result.Failures, failure => failure.StartsWith("CON-002:", StringComparison.Ordinal));
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
