using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Continuity;
using CommandCenter.Backend.Execution;
using CommandCenter.Backend.Planning;
using CommandCenter.Backend.Projections;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Tests;

public sealed class OperationalContextGenerationTests
{
    [Fact]
    public void ParserMapsCanonicalSections()
    {
        var parser = new MarkdownOperationalContextParser();

        var document = parser.Parse("""
            # Operational Context

            ## Current Mental Model

            - The repository owns continuity artifacts.

            ## Constraints

            - Human review is required before promotion.

            ## Active Risks

            - Context growth can hide durable decisions.
            """);

        Assert.Equal("Operational Context", document.Title);
        Assert.Single(document.CurrentMentalModel);
        Assert.Equal(OperationalContextItemKind.MentalModel, document.CurrentMentalModel[0].Kind);
        Assert.Single(document.Constraints);
        Assert.Equal(OperationalContextItemKind.Constraint, document.Constraints[0].Kind);
        Assert.Single(document.ActiveRisks);
        Assert.Equal(OperationalContextItemKind.ActiveRisk, document.ActiveRisks[0].Kind);
    }

    [Fact]
    public void ParserAndRendererPreserveUnknownSections()
    {
        var parser = new MarkdownOperationalContextParser();

        var document = parser.Parse("""
            # Operational Context

            ## Architecture

            - Backend services own workflow authority.

            ## Hand Written Notes

            Preserve this note exactly enough for reviewer inspection.
            """);

        var additionalSection = Assert.Single(document.AdditionalSections);
        Assert.Equal("Hand Written Notes", additionalSection.Heading);
        Assert.Contains("Preserve this note", additionalSection.Content);

        var rendered = parser.Render(document);

        Assert.Contains("## Hand Written Notes", rendered);
        Assert.Contains("Preserve this note exactly enough for reviewer inspection.", rendered);
    }

    [Fact]
    public void RendererEmitsStableCanonicalSectionOrder()
    {
        var parser = new MarkdownOperationalContextParser();

        var rendered = parser.Render(new OperationalContextDocument
        {
            Constraints =
            [
                new OperationalContextItem
                {
                    Id = "constraint-1",
                    Kind = OperationalContextItemKind.Constraint,
                    Text = "Do not mutate current understanding during generation."
                }
            ]
        });

        Assert.Contains("# Operational Context", rendered);
        Assert.True(rendered.IndexOf("## Current Mental Model", StringComparison.Ordinal) <
            rendered.IndexOf("## Architecture", StringComparison.Ordinal));
        Assert.True(rendered.IndexOf("## Constraints", StringComparison.Ordinal) <
            rendered.IndexOf("## Stable Decisions", StringComparison.Ordinal));
        Assert.Contains("- Do not mutate current understanding during generation.", rendered);
    }

    [Fact]
    public void DiffReportsCoarseItemChanges()
    {
        var parser = new MarkdownOperationalContextParser();
        var diff = new UnderstandingDiffService();
        var current = parser.Parse("""
            # Operational Context

            ## Constraints

            - Existing constraint.
            """);
        var proposed = parser.Parse("""
            # Operational Context

            ## Constraints

            - Existing constraint.
            - New constraint.
            """);

        var changes = diff.Compare(current, proposed);

        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.ConstraintAdded &&
            change.Description.Contains("New constraint", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GenerationSucceedsWithoutExistingOperationalContext()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m2.md", "# M2");
        await WriteAsync(harness.Repository, ".agents/handoffs/handoff.md", """
            # Handoff

            - Proposal persistence was added.
            """);

        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Equal(OperationalContextProposalStatus.Pending, proposal.Status);
        Assert.NotNull(proposal.GeneratedContent);
        Assert.Contains("## Current Mental Model", proposal.GeneratedContent);
        Assert.Contains("Latest handoff signal: Proposal persistence was added.", proposal.GeneratedContent);
        Assert.False(File.Exists(Path.Combine(harness.Repository.Path, ".agents", "operational_context.md")));
    }

    [Fact]
    public async Task GenerationUsesExistingContextAndPreservesUnknownSections()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Architecture

            - Existing architecture survives.

            ## Hand Written Notes

            Keep this local note.
            """);
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", """
            # Decisions

            - Proposal infrastructure has priority over generation quality.
            """);

        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Contains("Existing architecture survives.", proposal.GeneratedContent);
        Assert.Contains("Proposal infrastructure has priority over generation quality.", proposal.GeneratedContent);
        Assert.Contains("## Hand Written Notes", proposal.GeneratedContent);
        Assert.Contains("Keep this local note.", proposal.GeneratedContent);
    }

    [Fact]
    public async Task ProposalPersistsAcrossStoreRecreation()
    {
        var harness = await CreateHarnessAsync();
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        var reloadedStore = new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore());
        var reloaded = await reloadedStore.GetAsync(harness.Repository, proposal.ProposalId, includeContent: true);

        Assert.NotNull(reloaded);
        Assert.Equal(proposal.ProposalId, reloaded.ProposalId);
        Assert.Equal(proposal.GeneratedContentHash, reloaded.GeneratedContentHash);
        Assert.Contains("## Current Mental Model", reloaded.GeneratedContent);
    }

    [Fact]
    public async Task RegenerationSupersedesPreviousPendingProposal()
    {
        var harness = await CreateHarnessAsync();
        var first = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        var second = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        var proposals = await harness.ProposalStore.ListAsync(harness.Repository);

        Assert.NotEqual(first.ProposalId, second.ProposalId);
        Assert.Contains(proposals, proposal =>
            proposal.ProposalId == first.ProposalId &&
            proposal.Status == OperationalContextProposalStatus.Superseded);
        Assert.Contains(proposals, proposal =>
            proposal.ProposalId == second.ProposalId &&
            proposal.Status == OperationalContextProposalStatus.Pending);
    }

    [Fact]
    public async Task WorkspaceProjectionSurfacesLatestProposalSummary()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m2.md", "# M2");
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        var projectionService = new RepositoryProjectionService(
            harness.RepositoryService,
            new ArtifactService(new FileSystemArtifactStore()),
            new PlanningService(new FileSystemArtifactStore()),
            harness.ExecutionSessionService,
            harness.ProposalStore);

        var workspace = await projectionService.GetWorkspaceAsync(harness.Repository.Id);

        Assert.True(workspace.OperationalContextProposalSummary.PendingProposalExists);
        Assert.Equal(proposal.ProposalId, workspace.OperationalContextProposalSummary.LatestProposalId);
        Assert.Equal(OperationalContextProposalStatus.Pending, workspace.OperationalContextProposalSummary.Status);
        Assert.True(workspace.OperationalContextProposalSummary.SourceInputCount > 0);
        Assert.True(workspace.OperationalContextProposalSummary.ContentByteCount > 0);
    }

    [Fact]
    public async Task PendingProposalIsReviewableAndLoadsContent()
    {
        var harness = await CreateHarnessAsync();

        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        var loaded = await harness.ProposalStore.GetAsync(harness.Repository, proposal.ProposalId, includeContent: true);

        Assert.NotNull(loaded);
        Assert.Equal(OperationalContextReviewState.PendingReview, loaded.Review.ReviewState);
        Assert.NotNull(loaded.GeneratedContent);
        Assert.Null(loaded.EditedContent);
    }

    [Fact]
    public async Task EditingPersistsReviewerContentAndRecomputesSemanticChanges()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Existing constraint.
            """);
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        var edited = await harness.ReviewService.EditAsync(harness.Repository.Id, proposal.ProposalId, """
            # Operational Context

            ## Constraints

            - Existing constraint.
            - Reviewer-added constraint.
            """);

        Assert.Equal(OperationalContextProposalStatus.Edited, edited.Status);
        Assert.Equal(OperationalContextReviewState.Edited, edited.Review.ReviewState);
        Assert.Contains("Reviewer-added constraint.", edited.EditedContent);
        Assert.Contains(edited.SemanticChanges, change =>
            change.Type == OperationalContextSemanticChangeType.ConstraintAdded &&
            change.Description.Contains("Reviewer-added constraint", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AcceptRecordsReviewStateWithoutChangingCurrentContext()
    {
        var harness = await CreateHarnessAsync();
        var currentContext = """
            # Operational Context

            ## Architecture

            - Existing architecture.
            """;
        await WriteAsync(harness.Repository, ".agents/operational_context.md", currentContext);
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        var accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, "Looks right.");

        Assert.Equal(OperationalContextProposalStatus.Accepted, accepted.Status);
        Assert.Equal(OperationalContextReviewState.Accepted, accepted.Review.ReviewState);
        Assert.Equal("Looks right.", accepted.Review.ReviewNote);
        Assert.Equal(proposal.GeneratedContentHash, accepted.Review.ReviewedContentHash);
        Assert.Equal(currentContext, await ReadAsync(harness.Repository, ".agents/operational_context.md"));
    }

    [Fact]
    public async Task RejectRecordsReviewStateAndLeavesContentForAudit()
    {
        var harness = await CreateHarnessAsync();
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        var rejected = await harness.ReviewService.RejectAsync(harness.Repository.Id, proposal.ProposalId, "Not enough signal.");

        Assert.Equal(OperationalContextProposalStatus.Rejected, rejected.Status);
        Assert.Equal(OperationalContextReviewState.Rejected, rejected.Review.ReviewState);
        Assert.Equal("Not enough signal.", rejected.Review.ReviewNote);
        Assert.NotNull(rejected.GeneratedContent);
    }

    [Fact]
    public async Task AcceptFailsForMissingSupersededOrStaleProposal()
    {
        var harness = await CreateHarnessAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            harness.ReviewService.AcceptAsync(harness.Repository.Id, "missing", null));

        var first = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        _ = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.ReviewService.AcceptAsync(harness.Repository.Id, first.ProposalId, null));

        var staleHarness = await CreateHarnessAsync();
        await WriteAsync(staleHarness.Repository, ".agents/operational_context.md", "# Operational Context");
        var staleProposal = await staleHarness.GenerationService.GenerateAsync(staleHarness.Repository.Id);
        await WriteAsync(staleHarness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Changed after generation.
            """);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            staleHarness.ReviewService.AcceptAsync(staleHarness.Repository.Id, staleProposal.ProposalId, null));
        var reloaded = await staleHarness.ProposalStore.GetAsync(staleHarness.Repository, staleProposal.ProposalId);
        Assert.Equal(OperationalContextReviewState.Stale, reloaded?.Review.ReviewState);
    }

    [Fact]
    public async Task ReviewStateSurvivesStoreRecreation()
    {
        var harness = await CreateHarnessAsync();
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        var edited = await harness.ReviewService.EditAsync(harness.Repository.Id, proposal.ProposalId, "# Operational Context");

        var reloadedStore = new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore());
        var reloaded = await reloadedStore.GetAsync(harness.Repository, edited.ProposalId, includeContent: true);

        Assert.NotNull(reloaded);
        Assert.Equal(OperationalContextProposalStatus.Edited, reloaded.Status);
        Assert.Equal(OperationalContextReviewState.Edited, reloaded.Review.ReviewState);
        Assert.Equal("# Operational Context", reloaded.EditedContent);
    }

    private static async Task<Harness> CreateHarnessAsync(
        IReadOnlyList<ExecutionSessionSummary>? executionHistory = null)
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        var artifactStore = new FileSystemArtifactStore();
        var artifactService = new ArtifactService(artifactStore);
        var executionSessionService = new StaticExecutionSessionService(executionHistory ?? []);
        var proposalStore = new FileSystemOperationalContextProposalStore(artifactStore);
        var parser = new MarkdownOperationalContextParser();
        var generationService = new OperationalContextGenerationService(
            repositoryService,
            artifactService,
            new PlanningService(artifactStore),
            executionSessionService,
            parser,
            new UnderstandingDiffService(),
            proposalStore);
        var reviewService = new OperationalContextReviewService(
            repositoryService,
            artifactService,
            parser,
            new UnderstandingDiffService(),
            proposalStore);

        return new Harness(
            repository,
            repositoryService,
            executionSessionService,
            proposalStore,
            generationService,
            reviewService);
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        var path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, content);
    }

    private static async Task<string> ReadAsync(Repository repository, string relativePath)
    {
        return await File.ReadAllTextAsync(
            Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string CreateGitRepositoryDirectory()
    {
        var directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record Harness(
        Repository Repository,
        RepositoryService RepositoryService,
        StaticExecutionSessionService ExecutionSessionService,
        FileSystemOperationalContextProposalStore ProposalStore,
        OperationalContextGenerationService GenerationService,
        OperationalContextReviewService ReviewService);

    private sealed class StaticExecutionSessionService(IReadOnlyList<ExecutionSessionSummary> history)
        : IExecutionSessionService
    {
        public Task RecoverAsync()
        {
            return Task.CompletedTask;
        }

        public Task<RepositoryExecutionState> GetRepositoryStateAsync(Guid repositoryId)
        {
            return Task.FromResult(RepositoryExecutionState.Ready);
        }

        public Task<ExecutionSessionSummary?> GetActiveSessionAsync(Guid repositoryId)
        {
            return Task.FromResult<ExecutionSessionSummary?>(null);
        }

        public Task<ExecutionSessionSummary?> GetRepositorySessionSummaryAsync(Guid repositoryId)
        {
            return Task.FromResult(history.FirstOrDefault());
        }

        public Task<IReadOnlyList<ExecutionSessionSummary>> GetRepositorySessionHistoryAsync(Guid repositoryId, int limit = 10)
        {
            return Task.FromResult<IReadOnlyList<ExecutionSessionSummary>>(history.Take(limit).ToArray());
        }

        public Task<ExecutionSessionSummary> StartAsync(Guid repositoryId, ExecutionStartRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSession?> GetSessionAsync(Guid sessionId)
        {
            return Task.FromResult<ExecutionSession?>(null);
        }

        public Task<ExecutionSessionSummary> AcceptAsync(Guid sessionId, ExecutionAcceptanceRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> RejectAsync(Guid sessionId, ExecutionAcceptanceRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<CommitPreparation> PrepareCommitAsync(Guid sessionId)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> CommitAsync(Guid sessionId, CommitRequest request)
        {
            throw new NotSupportedException();
        }

        public Task<ExecutionSessionSummary> PushAsync(Guid sessionId, PushRequest request)
        {
            throw new NotSupportedException();
        }
    }
}
