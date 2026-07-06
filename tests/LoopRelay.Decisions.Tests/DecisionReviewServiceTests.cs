using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Decisions.Abstractions;
using LoopRelay.Decisions.Models;
using LoopRelay.Decisions.Primitives;
using LoopRelay.Decisions.Services;

namespace LoopRelay.Decisions.Tests;

[Collection("ProcessEnvironment")]
public sealed class DecisionReviewServiceTests
{
    [Fact]
    public async Task ReviewTransitionPersistsReviewStatusSeparateFromProposal()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionReviewService reviewService = CreateReviewService(repository, decisionRepository, generationService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);

        DecisionReviewWorkspace workspace = await reviewService.MarkProposalViewedAsync(
            repository.Id,
            proposal.Id,
            "Reviewer inspected the proposal.");

        DecisionReviewStatus? reviewStatus = await decisionRepository.GetReviewStatusAsync(repository, proposal.Id);
        DecisionProposal? reloadedProposal = await decisionRepository.GetProposalAsync(repository, proposal.Id);
        Assert.Equal(DecisionProposalState.Viewed, workspace.Proposal.State);
        Assert.Equal(DecisionReviewState.Viewed, workspace.Review.State);
        Assert.Equal(DecisionReviewState.Viewed, reviewStatus?.State);
        Assert.Equal(DecisionProposalState.Viewed, reloadedProposal?.State);
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "review.json")));
        Assert.True(File.Exists(Path.Combine(repository.Path, ".agents", "decisions", "proposals", "PROP-0001", "proposal.json")));
    }

    [Fact]
    public async Task ReviewNotesPersistSeparatelyFromProposalRevisionsAndProposalContent()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionReviewService reviewService = CreateReviewService(repository, decisionRepository, generationService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        string proposalJsonBefore = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.json");

        DecisionReviewNote note = await reviewService.AddReviewNoteAsync(
            repository.Id,
            proposal.Id,
            new DecisionReviewNoteRequest("Evidence needs a tighter source reference.", "human-reviewer"));

        string proposalJsonAfter = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/proposal.json");
        IReadOnlyList<DecisionReviewNote> notes = await reviewService.ListReviewNotesAsync(repository.Id, proposal.Id);
        IReadOnlyList<DecisionProposalRevision> revisions = await decisionRepository.ListProposalRevisionsAsync(repository, proposal.Id);
        string notesJson = await ReadAsync(repository, ".agents/decisions/proposals/PROP-0001/notes.json");

        Assert.Equal("NOTE-0001", note.Id);
        Assert.Equal("human-reviewer", note.Reviewer);
        Assert.Equal(proposalJsonBefore, proposalJsonAfter);
        Assert.Empty(revisions);
        Assert.Single(notes);
        Assert.Contains("Evidence needs a tighter source reference.", notesJson);
        Assert.DoesNotContain("Evidence needs a tighter source reference.", proposalJsonAfter);
    }

    [Fact]
    public async Task ReviewWorkspaceContainsProposalEvidenceNotesRevisionsAndDiagnostics()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionReviewService reviewService = CreateReviewService(repository, decisionRepository, generationService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await reviewService.MarkProposalViewedAsync(repository.Id, proposal.Id, null);
        await reviewService.MarkProposalNeedsRefinementAsync(repository.Id, proposal.Id, "Needs clearer context.");
        await reviewService.AddReviewNoteAsync(
            repository.Id,
            proposal.Id,
            new DecisionReviewNoteRequest("Clarify the accepted option evidence.", "human-reviewer"));
        await generationService.RefineProposalAsync(
            repository.Id,
            proposal.Id,
            new DecisionRefinementRequest("Clarify context.", Context: "Refined context for review."));

        DecisionReviewWorkspace workspace = await reviewService.GetReviewWorkspaceAsync(repository.Id, proposal.Id);

        Assert.Equal(DecisionProposalState.Refined, workspace.Proposal.State);
        Assert.Equal(DecisionReviewState.NeedsRefinement, workspace.Review.State);
        Assert.Single(workspace.Notes);
        Assert.Single(workspace.Revisions);
        Assert.True(workspace.Diagnostics.HasRecommendation);
        Assert.True(workspace.Diagnostics.HasEvidence);
        Assert.Equal(workspace.Proposal.Options.Count, workspace.Diagnostics.OptionCount);
        Assert.Empty(workspace.Diagnostics.Warnings);
    }

    [Fact]
    public async Task ReviewWorkspaceExposesProposalAndLatestPackageAuthority()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionReviewService reviewService = CreateReviewService(repository, decisionRepository, generationService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        DecisionPackageVersion package = Assert.Single(await decisionRepository.ListPackageVersionsAsync(repository, proposal.Id));
        DecisionReviewWorkspace workspace = await reviewService.MarkProposalReadyForResolutionAsync(
            repository.Id,
            proposal.Id,
            "Ready for authority inspection.");

        Assert.False(string.IsNullOrWhiteSpace(workspace.Authority.ProposalFingerprint));
        Assert.Equal(package.Id, workspace.Authority.PackageId);
        Assert.Equal(package.PackageFingerprint, workspace.Authority.PackageFingerprint);
        Assert.Equal(package.CreatedAt, workspace.Authority.PackageVersionCreatedAt);
        Assert.Equal(package.Package.Metadata.SourceProposalFingerprint, workspace.Authority.PackageSourceProposalFingerprint);
        Assert.True(workspace.Authority.IsPackageCurrentForProposalContent);
    }

    [Fact]
    public async Task ReviewReadModelsExposeBrowserComparisonEvidenceAndSources()
    {
        Repository repository = CreateRepository();
        DecisionCandidate candidate = CreateCandidate(repository.Id, DecisionCandidateState.Promoted);
        var store = new FileSystemArtifactStore();
        var decisionRepository = new FileSystemDecisionRepository(store);
        await decisionRepository.SaveCandidateAsync(repository, candidate);
        DecisionGenerationService generationService = CreateGenerationService(repository, store, decisionRepository);
        DecisionReviewService reviewService = CreateReviewService(repository, decisionRepository, generationService);
        DecisionProposal proposal = await generationService.GenerateProposalAsync(repository.Id, candidate.Id);
        await reviewService.MarkProposalViewedAsync(repository.Id, proposal.Id, "Read model review.");

        IReadOnlyList<DecisionProposalBrowserItem> browserItems = await reviewService.ListProposalBrowserItemsAsync(
            repository.Id,
            new HashSet<DecisionProposalState> { DecisionProposalState.Viewed });
        DecisionOptionComparison comparison = await reviewService.GetOptionComparisonAsync(repository.Id, proposal.Id);
        DecisionEvidenceInspection evidence = await reviewService.GetEvidenceInspectionAsync(repository.Id, proposal.Id);
        IReadOnlyList<DecisionSourceAttribution> sources = await reviewService.ListSourceAttributionsAsync(repository.Id, proposal.Id);

        DecisionProposalBrowserItem browserItem = Assert.Single(browserItems);
        Assert.Equal(proposal.Id, browserItem.ProposalId);
        Assert.Equal(DecisionProposalState.Viewed, browserItem.State);
        Assert.Equal(DecisionReviewState.Viewed, browserItem.ReviewState);
        Assert.Equal(DecisionClassification.Architectural, browserItem.Classification);
        Assert.Equal(DecisionCandidatePriority.High, browserItem.Priority);
        Assert.False(browserItem.IsResolved);

        Assert.True(comparison.Options.Count >= 3);
        DecisionOptionComparisonItem option = Assert.Single(comparison.Options, option => option.IsRecommended);
        Assert.True(option.IsRecommended);
        Assert.NotEmpty(option.Benefits);
        Assert.NotEmpty(option.Costs);
        Assert.NotEmpty(option.Evidence);

        Assert.Equal(proposal.Id, evidence.ProposalId);
        Assert.True(evidence.Diagnostics.HasRecommendation);
        Assert.Contains(evidence.Items, item => item.AppliesToKind == "Recommendation" && item.ItemId == "option-1");
        Assert.Contains(evidence.Items.SelectMany(item => item.Sources), source =>
            source.RelativePath == ".agents/plan.md" &&
            source.Section == "Plan" &&
            source.Excerpt == "Need to decide repository-backed persistence schema.");
        Assert.Contains(sources, source =>
            source.AppliesToKind == "Proposal" &&
            source.SourceKind == "DecisionProposal" &&
            source.RelativePath == ".agents/decisions/proposals/PROP-0001/proposal.json");
    }

    private static DecisionGenerationService CreateGenerationService(
        Repository repository,
        FileSystemArtifactStore store,
        FileSystemDecisionRepository decisionRepository)
    {
        var repositoryService = new StubRepositoryService(repository);
        var projectionService = new DecisionArtifactProjectionService(decisionRepository, store);
        return new DecisionGenerationService(
            repositoryService,
            decisionRepository,
            projectionService,
            new OptionGenerationService());
    }

    private static DecisionReviewService CreateReviewService(
        Repository repository,
        FileSystemDecisionRepository decisionRepository,
        DecisionGenerationService generationService)
    {
        return new DecisionReviewService(new StubRepositoryService(repository), decisionRepository, generationService);
    }

    private static DecisionCandidate CreateCandidate(Guid repositoryId, DecisionCandidateState state)
    {
        return new DecisionCandidate(
            "CAND-0001",
            repositoryId,
            state,
            DecisionCandidatePriority.High,
            DecisionClassification.Architectural,
            "Decide persistence schema",
            "Need to decide repository-backed persistence schema.",
            "source-fingerprint",
            [new DecisionSignal(
                "MissingDirection",
                "Need to decide repository-backed persistence schema.",
                DecisionClassification.Architectural,
                DecisionCandidatePriority.High,
                [new DecisionEvidence(
                    "Plan requires a persistence decision.",
                    [new DecisionSourceReference(
                        "Plan",
                        ".agents/plan.md",
                        Section: "Plan",
                        ItemId: "plan",
                        Excerpt: "Need to decide repository-backed persistence schema.")])])],
            [new DecisionEvidence(
                "Plan requires a persistence decision.",
                [new DecisionSourceReference(
                    "Plan",
                    ".agents/plan.md",
                    Section: "Plan",
                    ItemId: "plan",
                    Excerpt: "Need to decide repository-backed persistence schema.")])],
            [new DecisionSourceReference(
                "Plan",
                ".agents/plan.md",
                Section: "Plan",
                ItemId: "plan",
                Excerpt: "Need to decide repository-backed persistence schema.")],
            ["Created by review test."],
            [new DecisionHistoryEntry(
                DateTimeOffset.UtcNow,
                state == DecisionCandidateState.Promoted ? "Promoted" : "Discovered",
                null,
                state.ToString(),
                "Seeded by review test.",
                [])]);
    }

    private static async Task<string> ReadAsync(Repository repository, string relativePath)
    {
        return await File.ReadAllTextAsync(Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static Repository CreateRepository()
    {
        string path = Path.Combine(Path.GetTempPath(), "LoopRelay.Tests", Guid.NewGuid().ToString("N"));
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
