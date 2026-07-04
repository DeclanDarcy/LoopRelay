using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Abstractions;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;

namespace CommandCenter.Decisions.Tests;

[Collection("ProcessEnvironment")]
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
