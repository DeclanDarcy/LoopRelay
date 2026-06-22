using System.Security.Cryptography;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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

public sealed class DecisionCertificationServiceTests
{
    [Fact]
    public async Task RunCertificationPersistsRepositoryBackedReport()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        DecisionCertificationService service = CreateService(repository, decisionRepository);

        DecisionCertificationReport report = await service.RunCertificationAsync(repository.Id);

        Assert.StartsWith("certification.", report.Id, StringComparison.Ordinal);
        Assert.Equal(DecisionLifecycleCertificationResultKind.Passed, report.Result.Kind);
        Assert.True(File.Exists(Path.Combine(
            repository.Path,
            ".agents",
            "decisions",
            "certification",
            $"{report.Id}.json")));
        Assert.Single(await decisionRepository.ListCertificationReportsAsync(repository));
    }

    [Fact]
    public async Task GetCurrentCertificationDoesNotPersistReport()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        DecisionCertificationService service = CreateService(repository, decisionRepository);

        DecisionCertificationReport report = await service.GetCurrentCertificationAsync(repository.Id);

        Assert.Equal(DecisionLifecycleCertificationResultKind.Passed, report.Result.Kind);
        Assert.Empty(await decisionRepository.ListCertificationReportsAsync(repository));
        Assert.False(Directory.Exists(Path.Combine(repository.Path, ".agents", "decisions", "certification")));
    }

    [Fact]
    public async Task CertificationReportIsReproducibleAcrossCurrentAndPersistedRunsWhenRepositoryIsUnchanged()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        await decisionRepository.SaveDecisionAsync(repository, CreateResolvedDecision(repository.Id));
        DecisionCertificationService service = CreateService(repository, decisionRepository);

        DecisionCertificationReport persisted = await service.RunCertificationAsync(repository.Id);
        DecisionCertificationReport current = await service.GetCurrentCertificationAsync(repository.Id);

        Assert.NotEqual(persisted.Id, current.Id);
        Assert.Equal(persisted.RepositoryId, current.RepositoryId);
        Assert.Equal(persisted.InputFingerprint, current.InputFingerprint);
        Assert.Equal(persisted.Result, current.Result);
        Assert.Equal(persisted.Health, current.Health);
        AssertCertificationEvidenceEquivalent(persisted.Evidence, current.Evidence);
        Assert.Equal(persisted.Diagnostics, current.Diagnostics);
        Assert.Single(await decisionRepository.ListCertificationReportsAsync(repository));
    }

    [Fact]
    public async Task CertificationFailsWhenResolvedDecisionClaimsSystemAuthority()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        await decisionRepository.SaveDecisionAsync(
            repository,
            CreateResolvedDecision(repository.Id) with
            {
                Resolution = CreateResolvedDecision(repository.Id).Resolution! with
                {
                    ResolvedBy = "governance"
                }
            });
        DecisionCertificationService service = CreateService(repository, decisionRepository);

        DecisionCertificationReport report = await service.GetCurrentCertificationAsync(repository.Id);

        Assert.Equal(DecisionLifecycleCertificationResultKind.Failed, report.Result.Kind);
        Assert.Contains(report.Evidence, evidence =>
            evidence.Id == "authority-boundaries" &&
            !evidence.Passed &&
            evidence.RelatedDecisionIds.SequenceEqual(["DEC-0001"]));
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    public async Task CertificationRecordsLongHorizonThresholdDiagnostics(int decisionCount)
    {
        Repository repository = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();
        for (int index = 1; index <= decisionCount; index++)
        {
            await decisionRepository.SaveDecisionAsync(
                repository,
                CreateResolvedDecision(
                    repository.Id,
                    $"DEC-{index:0000}",
                    $"Use repository-backed decision {index}",
                    $"CAND-{index:0000}",
                    $"PROP-{index:0000}"));
        }

        DecisionCertificationService service = CreateService(repository, decisionRepository);

        DecisionCertificationReport report = await service.GetCurrentCertificationAsync(repository.Id);

        Assert.Equal(DecisionLifecycleCertificationResultKind.Passed, report.Result.Kind);
        Assert.Contains($"Long-horizon certification reached the {decisionCount}-decision fixture threshold.", report.Diagnostics);
        Assert.Contains(report.Evidence, evidence =>
            evidence.Id == "long-horizon-histories" &&
            evidence.Passed &&
            evidence.RelatedDecisionIds.Count == decisionCount);
    }

    [Fact]
    public async Task CertificationEndpointsReturnCurrentReportPersistedRunAndHistory()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\nDecision lifecycle certification.");

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        HttpResponseMessage currentResponse =
            await client.GetAsync($"{root}/api/repositories/{repository.Id}/decisions/certification");
        HttpResponseMessage runResponse =
            await client.PostAsync($"{root}/api/repositories/{repository.Id}/decisions/certification", null);
        HttpResponseMessage historyResponse =
            await client.GetAsync($"{root}/api/repositories/{repository.Id}/decisions/certification/reports");

        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        DecisionCertificationReport? current =
            await currentResponse.Content.ReadFromJsonAsync<DecisionCertificationReport>(jsonOptions);
        DecisionCertificationReport? persisted =
            await runResponse.Content.ReadFromJsonAsync<DecisionCertificationReport>(jsonOptions);
        DecisionCertificationReport[]? history =
            await historyResponse.Content.ReadFromJsonAsync<DecisionCertificationReport[]>(jsonOptions);
        Assert.NotNull(current);
        Assert.NotNull(persisted);
        Assert.NotNull(history);
        Assert.Equal(DecisionLifecycleCertificationResultKind.Passed, current.Result.Kind);
        Assert.Equal(DecisionLifecycleCertificationResultKind.Passed, persisted.Result.Kind);
        Assert.StartsWith("certification.", persisted.Id, StringComparison.Ordinal);
        Assert.Single(history);
        Assert.Equal(persisted.Id, history[0].Id);
    }

    [Fact]
    public async Task CertificationEndpointPassesAfterEndToEndDecisionLifecycle()
    {
        Repository repository = CreateRepository();
        await WriteAsync(repository, ".agents/plan.md", "# Plan\n\n- Need to decide repository-backed persistence schema.");
        await WriteAsync(repository, ".agents/milestones/m9-lifecycle-certification.md", "# M9\n\n- Certify lifecycle.");
        await WriteAsync(repository, ".agents/operational_context.md", "# Operational Context\n\nStable understanding.");

        await using WebApplication app = Program.CreateApp(
            [],
            services => services.AddSingleton<IRepositoryService>(new StubRepositoryService(repository)));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient();
        string root = app.Urls.Single();
        JsonSerializerOptions jsonOptions = CreateJsonOptions();

        DecisionDiscoveryResult discovery = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/discover",
            null)).Content.ReadFromJsonAsync<DecisionDiscoveryResult>(jsonOptions))!;
        DecisionCandidate candidate = Assert.Single(discovery.Candidates);
        await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/promote",
            new DecisionCandidateTransitionRequest("Ready for proposal."),
            jsonOptions);
        DecisionProposal proposal = (await (await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/candidates/{candidate.Id}/proposals",
            null)).Content.ReadFromJsonAsync<DecisionProposal>(jsonOptions))!;
        await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/review/ready-for-resolution",
            new DecisionProposalTransitionRequest("Ready for human resolution."),
            jsonOptions);
        Decision decision = (await (await client.PostAsJsonAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/proposals/{proposal.Id}/resolve",
            new ResolveDecisionCommand("Accept repository-backed decision lifecycle.", "human-reviewer", "option-1"),
            jsonOptions)).Content.ReadFromJsonAsync<Decision>(jsonOptions))!;

        HttpResponseMessage governanceResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/governance/reports",
            null);
        HttpResponseMessage projectionResponse = await client.GetAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/execution-projection");
        HttpResponseMessage certificationResponse = await client.PostAsync(
            $"{root}/api/repositories/{repository.Id}/decisions/certification",
            null);

        DecisionGovernanceReport governance =
            (await governanceResponse.Content.ReadFromJsonAsync<DecisionGovernanceReport>(jsonOptions))!;
        ExecutionDecisionProjection projection =
            (await projectionResponse.Content.ReadFromJsonAsync<ExecutionDecisionProjection>(jsonOptions))!;
        DecisionCertificationReport certification =
            (await certificationResponse.Content.ReadFromJsonAsync<DecisionCertificationReport>(jsonOptions))!;
        Assert.Equal(HttpStatusCode.OK, governanceResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, projectionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, certificationResponse.StatusCode);
        Assert.Equal(DecisionState.Resolved, decision.State);
        Assert.DoesNotContain(governance.Findings, finding => finding.BlocksExecutionProjection);
        Assert.True(projection.Directives.Count + projection.Constraints.Count > 0);
        Assert.Equal(DecisionLifecycleCertificationResultKind.Passed, certification.Result.Kind);
        Assert.All(certification.Evidence, evidence => Assert.True(evidence.Passed, evidence.Id));
        Assert.Contains(certification.Evidence, evidence =>
            evidence.Id == "execution-consumption" &&
            evidence.RelatedDecisionIds.Contains(decision.Id.Value, StringComparer.Ordinal));
    }

    private static DecisionCertificationService CreateService(
        Repository repository,
        IDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var governanceService = new DecisionGovernanceService(repositoryService, decisionRepository);
        var projectionService = new DecisionProjectionService(repositoryService, decisionRepository, governanceService);
        return new DecisionCertificationService(repositoryService, decisionRepository, governanceService, projectionService);
    }

    private static Decision CreateResolvedDecision(
        Guid repositoryId,
        string decisionId = "DEC-0001",
        string title = "Use repository-backed decisions",
        string candidateId = "CAND-0001",
        string proposalId = "PROP-0001")
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
            [new DecisionOption("option-1", title, "Persist records under .agents/decisions.", [])],
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

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static void AssertCertificationEvidenceEquivalent(
        IReadOnlyList<DecisionCertificationEvidence> expected,
        IReadOnlyList<DecisionCertificationEvidence> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (DecisionCertificationEvidence expectedEvidence in expected)
        {
            DecisionCertificationEvidence actualEvidence = Assert.Single(
                actual,
                evidence => evidence.Id == expectedEvidence.Id);
            Assert.Equal(expectedEvidence.Area, actualEvidence.Area);
            Assert.Equal(expectedEvidence.Passed, actualEvidence.Passed);
            Assert.Equal(expectedEvidence.Detail, actualEvidence.Detail);
            Assert.Equal(expectedEvidence.RelatedDecisionIds, actualEvidence.RelatedDecisionIds);
            Assert.Equal(expectedEvidence.RelatedCandidateIds, actualEvidence.RelatedCandidateIds);
            Assert.Equal(expectedEvidence.RelatedProposalIds, actualEvidence.RelatedProposalIds);
            Assert.Equal(
                expectedEvidence.Sources.Select(SourceKey).Order(StringComparer.Ordinal),
                actualEvidence.Sources.Select(SourceKey).Order(StringComparer.Ordinal));
        }
    }

    private static string SourceKey(DecisionSourceReference source)
    {
        return string.Join(
            "|",
            source.SourceKind,
            source.RelativePath,
            source.Section,
            source.ItemId,
            source.DecisionId?.Value,
            source.ProposalId,
            source.CandidateId,
            source.Excerpt);
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
