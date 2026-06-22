using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionRepositoryTests
{
    [Fact]
    public async Task FileSystemRepositoryAllocatesIdsByScanningExistingArtifacts()
    {
        Repository repository = CreateRepository();
        Directory.CreateDirectory(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001"));
        Directory.CreateDirectory(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0009"));
        Directory.CreateDirectory(Path.Combine(repository.Path, ".agents", "decisions", "candidates", "CAND-0004"));
        Directory.CreateDirectory(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0007"));
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());

        DecisionId decisionId = await decisionRepository.AllocateDecisionIdAsync(repository);
        string candidateId = await decisionRepository.AllocateCandidateIdAsync(repository);
        string proposalId = await decisionRepository.AllocateProposalIdAsync(repository);

        Assert.Equal("DEC-0010", decisionId.Value);
        Assert.Equal("CAND-0005", candidateId);
        Assert.Equal("PROP-0008", proposalId);
    }

    [Fact]
    public async Task FileSystemRepositoryRoundTripsDecisionCandidateAndProposal()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Decision decision = CreateDecision(repository.Id, now);
        DecisionCandidate candidate = CreateCandidate(repository.Id);
        DecisionProposal proposal = CreateProposal(repository.Id);

        await decisionRepository.SaveDecisionAsync(repository, decision);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        await decisionRepository.SaveProposalAsync(repository, proposal);

        Decision? reloadedDecision = await decisionRepository.GetDecisionAsync(repository, decision.Id);
        DecisionCandidate? reloadedCandidate = await decisionRepository.GetCandidateAsync(repository, candidate.Id);
        DecisionProposal? reloadedProposal = await decisionRepository.GetProposalAsync(repository, proposal.Id);

        Assert.NotNull(reloadedDecision);
        Assert.Equal(decision.Id, reloadedDecision.Id);
        Assert.Equal(decision.State, reloadedDecision.State);
        Assert.Equal(decision.Metadata.RepositoryId, reloadedDecision.Metadata.RepositoryId);
        Assert.Single(reloadedDecision.Evidence);
        Assert.Single(reloadedDecision.History);
        Assert.NotNull(reloadedCandidate);
        Assert.Equal(candidate.Id, reloadedCandidate.Id);
        Assert.Equal(candidate.RepositoryId, reloadedCandidate.RepositoryId);
        Assert.Single(reloadedCandidate.Sources);
        Assert.NotNull(reloadedProposal);
        Assert.Equal(proposal.Id, reloadedProposal.Id);
        Assert.Equal(proposal.RepositoryId, reloadedProposal.RepositoryId);
        Assert.Single(reloadedProposal.Options);
        Assert.Single(reloadedProposal.Tradeoffs);
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "history.json")));
        Assert.Contains("\"schemaVersion\": \"1\"", await File.ReadAllTextAsync(
            Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "decision.json")));
        Assert.Contains(repository.Id.ToString(), await File.ReadAllTextAsync(
            Path.Combine(repository.Path, ".agents", "decisions", "candidates", "CAND-0001", "candidate.json")));
    }

    [Fact]
    public async Task FileSystemRepositoryIsolatesRepositories()
    {
        Repository first = CreateRepository();
        Repository second = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        Decision firstDecision = CreateDecision(first.Id, DateTimeOffset.UtcNow);
        Decision secondDecision = CreateDecision(second.Id, DateTimeOffset.UtcNow);

        await decisionRepository.SaveDecisionAsync(first, firstDecision);
        await decisionRepository.SaveDecisionAsync(second, secondDecision with { Title = "Second repository decision" });

        Decision? loadedFromFirst = await decisionRepository.GetDecisionAsync(first, new DecisionId("DEC-0001"));
        Decision? loadedFromSecond = await decisionRepository.GetDecisionAsync(second, new DecisionId("DEC-0001"));

        Assert.Equal("Persist structured decision records", loadedFromFirst?.Title);
        Assert.Equal("Second repository decision", loadedFromSecond?.Title);
    }

    [Fact]
    public async Task FileSystemRepositoryRejectsCrossRepositoryOwnership()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        Decision decision = CreateDecision(Guid.NewGuid(), DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            decisionRepository.SaveDecisionAsync(repository, decision));
    }

    [Fact]
    public async Task FileSystemRepositoryRejectsUnsupportedSchemaVersion()
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());
        Decision decision = CreateDecision(repository.Id, DateTimeOffset.UtcNow);
        string path = Path.Combine(repository.Path, ".agents", "decisions", "records", "DEC-0001", "decision.json");

        await decisionRepository.SaveDecisionAsync(repository, decision);
        string content = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, content.Replace("\"schemaVersion\": \"1\"", "\"schemaVersion\": \"2\""));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            decisionRepository.GetDecisionAsync(repository, decision.Id));
    }

    [Theory]
    [InlineData("../DEC-0001")]
    [InlineData("DEC-0001/extra")]
    [InlineData("dec-0001")]
    [InlineData("DEC-1")]
    public async Task FileSystemRepositoryRejectsUnsafeDecisionIds(string id)
    {
        Repository repository = CreateRepository();
        var decisionRepository = new FileSystemDecisionRepository(new FileSystemArtifactStore());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            decisionRepository.GetDecisionAsync(repository, new DecisionId(id)));
    }

    [Fact]
    public async Task InMemoryRepositoryAllocatesIdsPerRepository()
    {
        Repository first = CreateRepository();
        Repository second = CreateRepository();
        var decisionRepository = new InMemoryDecisionRepository();

        await decisionRepository.SaveCandidateAsync(first, CreateCandidate(first.Id));

        Assert.Equal("CAND-0002", await decisionRepository.AllocateCandidateIdAsync(first));
        Assert.Equal("CAND-0001", await decisionRepository.AllocateCandidateIdAsync(second));
    }

    private static Decision CreateDecision(Guid repositoryId, DateTimeOffset now)
    {
        return new Decision(
            new DecisionId("DEC-0001"),
            DecisionState.Open,
            DecisionClassification.Architectural,
            "Persist structured decision records",
            "Decision lifecycle needs repository-owned state.",
            new DecisionMetadata(repositoryId, now, now),
            null,
            [],
            [new DecisionEvidence("M0B requires structured artifacts.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            [new DecisionHistoryEntry(now, "Created", null, DecisionState.Open.ToString(), "Initial persistence test.", [])]);
    }

    private static DecisionCandidate CreateCandidate(Guid repositoryId)
    {
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            DecisionCandidateState.Discovered,
            DecisionCandidatePriority.High,
            "Persist decisions",
            "The plan calls for authoritative structured decision artifacts.",
            "source-fingerprint",
            [new DecisionSourceReference("Plan", ".agents/plan.md", Excerpt: "repository-backed persistence")],
            [new DecisionHistoryEntry(DateTimeOffset.UtcNow, "Discovered", null, DecisionCandidateState.Discovered.ToString(), null, [])]);
    }

    private static DecisionProposal CreateProposal(Guid repositoryId)
    {
        return new DecisionProposal(
            "PROP-0001",
            repositoryId,
            "CAND-0001",
            DecisionProposalState.Generated,
            "Persist decisions as structured JSON",
            "Use repository-owned JSON records with deterministic serialization.",
            [new DecisionOption("option-1", "Use structured files", "Store decision lifecycle records under .agents/decisions.", [])],
            [new DecisionTradeoff("option-1", "Recoverable from repository artifacts.", "Requires schema validation.", [])],
            new DecisionRecommendation("option-1", "Matches the repository authority rule.", []),
            [new DecisionAssumption("assumption-1", "Markdown projections are generated later.", [])],
            [new DecisionEvidence("M0B defers markdown projections.", [])],
            [new DecisionHistoryEntry(DateTimeOffset.UtcNow, "Generated", null, DecisionProposalState.Generated.ToString(), null, [])]);
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(path),
            Path = path
        };
    }
}
