using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Repositories;
using CommandCenter.Decisions.Models;
using CommandCenter.Decisions.Primitives;
using CommandCenter.Decisions.Services;

namespace CommandCenter.Backend.Tests;

public sealed class DecisionArtifactProjectionServiceTests
{
    [Fact]
    public async Task RefreshAllRendersLifecycleMarkdownProjections()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        DateTimeOffset now = new(2026, 06, 22, 12, 00, 00, TimeSpan.Zero);

        await decisionRepository.SaveDecisionAsync(repository, CreateDecision(repository.Id, now));
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, now));
        await decisionRepository.SaveProposalAsync(repository, CreateProposal(repository.Id, now));

        await projectionService.RefreshAllAsync(repository);

        string decisionMarkdown = await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.md");
        string candidateMarkdown = await ReadAsync(repository, ".agents/decisions/candidates/CAND-0001/candidate.md");
        string proposalMarkdown = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");

        Assert.Contains("# DEC-0001: Persist structured decision records", decisionMarkdown);
        Assert.Contains("- State: Open", decisionMarkdown);
        Assert.Contains("## Evidence", decisionMarkdown);
        Assert.Contains("- M0B requires structured artifacts.", decisionMarkdown);
        Assert.Contains("# CAND-0001: Persist decisions", candidateMarkdown);
        Assert.Contains("- Source fingerprint: source-fingerprint", candidateMarkdown);
        Assert.Contains("- Plan; path: .agents/plan.md; excerpt: repository-backed persistence", candidateMarkdown);
        Assert.Contains("# PROP-0001: Persist decisions as structured JSON", proposalMarkdown);
        Assert.Contains("### option-1: Use structured files", proposalMarkdown);
        Assert.Contains("- Option option-1: benefit Recoverable from repository artifacts.; cost Requires schema validation.", proposalMarkdown);
        Assert.Contains("## Recommendation", proposalMarkdown);
    }

    [Fact]
    public async Task RefreshDecisionIndexRendersDeterministicCurrentDecisionIndex()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        DateTimeOffset now = new(2026, 06, 22, 12, 00, 00, TimeSpan.Zero);

        await decisionRepository.SaveDecisionAsync(repository, CreateDecision(repository.Id, now));
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, now));
        await decisionRepository.SaveProposalAsync(repository, CreateProposal(repository.Id, now));

        await projectionService.RefreshDecisionIndexAsync(repository);
        string first = await ReadAsync(repository, ".agents/decisions/decisions.md");
        await projectionService.RefreshDecisionIndexAsync(repository);
        string second = await ReadAsync(repository, ".agents/decisions/decisions.md");

        Assert.Equal(first, second);
        Assert.Contains("Generated from structured decision lifecycle artifacts. Structured JSON remains authoritative.", first);
        Assert.Contains("- DEC-0001 | Open | Architectural | Unresolved | Persist structured decision records", first);
        Assert.Contains("- CAND-0001 | Discovered | High | Architectural | Persist decisions", first);
        Assert.Contains("- PROP-0001 | Generated | CAND-0001 | Persist decisions as structured JSON", first);
    }

    [Fact]
    public async Task GeneratedIndexPreservesExistingDecisionArtifactDiscoveryCompatibility()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        var artifactService = new ArtifactService(store);

        await decisionRepository.SaveDecisionAsync(repository, CreateDecision(repository.Id, DateTimeOffset.UtcNow));
        await projectionService.RefreshAllAsync(repository);

        IReadOnlyList<Artifact> artifacts = await artifactService.DiscoverAsync(repository);

        Assert.Contains(artifacts, artifact => artifact.RelativePath == ".agents/decisions/decisions.md" && artifact.VersionKind == ArtifactVersionKind.Current);
        Assert.DoesNotContain(artifacts, artifact => artifact.RelativePath.EndsWith("decision.json", StringComparison.Ordinal));
        Assert.DoesNotContain(artifacts, artifact => artifact.RelativePath.EndsWith("candidate.json", StringComparison.Ordinal));
        Assert.DoesNotContain(artifacts, artifact => artifact.RelativePath.EndsWith("proposal.json", StringComparison.Ordinal));
        Assert.DoesNotContain(artifacts, artifact => artifact.RelativePath.EndsWith("history.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GeneratedIndexCanBeRotatedWithExistingRotationService()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        var artifactService = new ArtifactService(store);
        var rotationService = new ArtifactRotationService(store, artifactService);

        await decisionRepository.SaveDecisionAsync(repository, CreateDecision(repository.Id, DateTimeOffset.UtcNow));
        await projectionService.RefreshDecisionIndexAsync(repository);
        string generatedIndex = await ReadAsync(repository, ".agents/decisions/decisions.md");

        Artifact rotated = await rotationService.RotateCurrentDecisionsAsync(repository);

        Assert.Equal(".agents/decisions/decisions.0001.md", rotated.RelativePath);
        Assert.Equal(generatedIndex, await ReadAsync(repository, ".agents/decisions/decisions.0001.md"));
        Assert.Equal(generatedIndex, await ReadAsync(repository, ".agents/decisions/decisions.md"));
    }

    [Fact]
    public async Task RecoverMissingProjectionsRegeneratesMarkdownFromStructuredArtifacts()
    {
        Repository repository = CreateRepository();
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        DateTimeOffset now = new(2026, 06, 22, 12, 00, 00, TimeSpan.Zero);

        await decisionRepository.SaveDecisionAsync(repository, CreateDecision(repository.Id, now));
        await decisionRepository.SaveCandidateAsync(repository, CreateCandidate(repository.Id, now));
        await decisionRepository.SaveProposalAsync(repository, CreateProposal(repository.Id, now));
        await projectionService.RefreshAllAsync(repository);

        string expectedDecision = await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.md");
        string expectedCandidate = await ReadAsync(repository, ".agents/decisions/candidates/CAND-0001/candidate.md");
        string expectedProposal = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        string expectedIndex = await ReadAsync(repository, ".agents/decisions/decisions.md");
        Delete(repository, ".agents/decisions/records/DEC-0001/decision.md");
        Delete(repository, ".agents/decisions/candidates/CAND-0001/candidate.md");
        Delete(repository, ".agents/decisions/proposals/PROP-0001/proposal.md");
        Delete(repository, ".agents/decisions/decisions.md");

        var restartedRepository = new FileSystemDecisionRepository(store);
        var restartedProjectionService = new DecisionArtifactProjectionService(restartedRepository, store);
        await restartedProjectionService.RecoverMissingProjectionsAsync(repository);

        Assert.Equal(expectedDecision, await ReadAsync(repository, ".agents/decisions/records/DEC-0001/decision.md"));
        Assert.Equal(expectedCandidate, await ReadAsync(repository, ".agents/decisions/candidates/CAND-0001/candidate.md"));
        Assert.Equal(expectedProposal, await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.md"));
        Assert.Equal(expectedIndex, await ReadAsync(repository, ".agents/decisions/decisions.md"));
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
            [new DecisionHistoryEntry(now, "Created", null, DecisionState.Open.ToString(), "Initial projection test.", [])]);
    }

    private static DecisionCandidate CreateCandidate(Guid repositoryId, DateTimeOffset now)
    {
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            DecisionCandidateState.Discovered,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Persist decisions",
            "The plan calls for authoritative structured decision artifacts.",
            "source-fingerprint",
            [new DecisionSignal(
                "MissingDirection",
                "A persistence decision is required.",
                DecisionClassification.Architectural,
                DecisionCandidatePriority.High,
                [new DecisionEvidence("Plan requires persistence.", [new DecisionSourceReference("Plan", ".agents/plan.md")])])],
            [new DecisionEvidence("Plan requires persistence.", [new DecisionSourceReference("Plan", ".agents/plan.md")])],
            [new DecisionSourceReference("Plan", ".agents/plan.md", Excerpt: "repository-backed persistence")],
            ["Created by projection test."],
            [new DecisionHistoryEntry(now, "Discovered", null, DecisionCandidateState.Discovered.ToString(), null, [])]);
    }

    private static DecisionProposal CreateProposal(Guid repositoryId, DateTimeOffset now)
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
            [new DecisionHistoryEntry(now, "Generated", null, DecisionProposalState.Generated.ToString(), null, [])]);
    }

    private static async Task<string> ReadAsync(Repository repository, string relativePath)
    {
        return await File.ReadAllTextAsync(Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void Delete(Repository repository, string relativePath)
    {
        File.Delete(Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar)));
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
}
