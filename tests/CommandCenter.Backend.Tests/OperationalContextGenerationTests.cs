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
    public async Task CompressionPreservesDurableUnderstandingAndReportsTiers()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Current Mental Model

            - Repository artifacts are authoritative.

            ## Architecture

            - Backend services own workflow authority.

            ## Constraints

            - Human review is mandatory before promotion.

            ## Stable Decisions

            - Proposals are repository-owned artifacts.

            ## Open Questions

            - Should diagnostics include growth trends?

            ## Active Risks

            - Context growth can hide important constraints.
            """);

        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Contains("Backend services own workflow authority.", proposal.GeneratedContent);
        Assert.Contains("Human review is mandatory before promotion.", proposal.GeneratedContent);
        Assert.Contains("Should diagnostics include growth trends?", proposal.GeneratedContent);
        Assert.Contains("Context growth can hide important constraints.", proposal.GeneratedContent);
        Assert.True(proposal.CompressionSummary.PermanentUnderstandingItemCount >= 4);
        Assert.True(proposal.CompressionSummary.ActiveUnderstandingItemCount >= 2);
        Assert.Empty(proposal.CompressionSummary.StableUnderstandingRetentionWarnings);
    }

    [Fact]
    public void CompressionFlagsAccidentalLossOfStableUnderstanding()
    {
        var parser = new MarkdownOperationalContextParser();
        var compression = new UnderstandingCompressionService();
        var current = parser.Parse("""
            # Operational Context

            ## Architecture

            - Backend services own workflow authority.

            ## Constraints

            - Human review is mandatory before promotion.

            ## Open Questions

            - Should diagnostics include growth trends?

            ## Active Risks

            - Context growth can hide important constraints.
            """);
        var proposed = parser.Parse("# Operational Context");

        var result = compression.Compress(current, proposed);

        Assert.Contains(result.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Architecture disappeared", StringComparison.Ordinal));
        Assert.Contains(result.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Constraint disappeared", StringComparison.Ordinal));
        Assert.Contains(result.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Open question disappeared", StringComparison.Ordinal));
        Assert.Contains(result.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Active risk disappeared", StringComparison.Ordinal));
    }

    [Fact]
    public async Task HistoricalNoiseDoesNotAccumulateInGeneratedProposal()
    {
        var harness = await CreateHarnessAsync();
        var recentChanges = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 20).Select(index => $"- Recent execution for `.agents/milestones/m{index}.md` is recorded with state `Completed`."));
        await WriteAsync(harness.Repository, ".agents/operational_context.md", $"""
            # Operational Context

            ## Recent Understanding Changes

            {recentChanges}
            """);

        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        var generated = new MarkdownOperationalContextParser().Parse(proposal.GeneratedContent ?? string.Empty);

        Assert.True(generated.RecentUnderstandingChanges.Count <= 12);
        Assert.True(proposal.CompressionSummary.CompressedItemCount > 0);
        Assert.NotEmpty(proposal.CompressionSummary.NoiseRemovedIndicators);
    }

    [Fact]
    public async Task DurableUnderstandingSurvivesRepeatedGeneratedRevisions()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Architecture

            - Backend services own continuity workflow authority.

            ## Constraints

            - Compression must never reduce authority.

            ## Stable Decisions

            - Repository artifacts remain authoritative.

            ## Decision Rationale

            - Disposable execution sessions must not become project memory.

            ## Open Questions

            - Should diagnostics include retention trends?

            ## Active Risks

            - Context growth can hide important constraints.
            """);

        for (var index = 0; index < 3; index++)
        {
            await WriteAsync(harness.Repository, ".agents/handoffs/handoff.md", $"""
                # Handoff

                - Slice {index} completed without changing authority boundaries.
                - Recent execution for `.agents/milestones/m5.md` is recorded with state `Completed`.
                """);

            var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
            var accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);
            await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);
        }

        var current = new MarkdownOperationalContextParser().Parse(
            await ReadAsync(harness.Repository, ".agents/operational_context.md"));

        Assert.Contains(current.Architecture, item =>
            item.Text.Contains("Backend services own continuity workflow authority", StringComparison.Ordinal));
        Assert.Contains(current.Constraints, item =>
            item.Text.Contains("Compression must never reduce authority", StringComparison.Ordinal));
        Assert.Contains(current.StableDecisions, item =>
            item.Text.Contains("Repository artifacts remain authoritative", StringComparison.Ordinal));
        Assert.Contains(current.DecisionRationale, item =>
            item.Text.Contains("Disposable execution sessions", StringComparison.Ordinal));
        Assert.Contains(current.OpenQuestions, item =>
            item.Text.Contains("retention trends", StringComparison.Ordinal));
        Assert.Contains(current.ActiveRisks, item =>
            item.Text.Contains("Context growth", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvedQuestionsCompressOnlyWithExplicitResolutionEvidence()
    {
        var parser = new MarkdownOperationalContextParser();
        var compression = new UnderstandingCompressionService();
        var current = parser.Parse("""
            # Operational Context

            ## Open Questions

            - Should diagnostics include growth trends?
            """);
        var proposedWithoutEvidence = parser.Parse("""
            # Operational Context

            ## Recent Understanding Changes

            - Diagnostics were discussed.
            """);
        var proposedWithEvidence = parser.Parse("""
            # Operational Context

            ## Open Questions

            - Should diagnostics include growth trends?

            ## Recent Understanding Changes

            - Resolved question: diagnostics include growth trends.
            """);

        var missingWithoutEvidence = compression.Compress(current, proposedWithoutEvidence);
        var resolvedWithEvidence = compression.Compress(current, proposedWithEvidence);

        Assert.Contains(missingWithoutEvidence.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Open question disappeared", StringComparison.Ordinal));
        Assert.Empty(resolvedWithEvidence.Document.OpenQuestions);
        Assert.Equal(1, resolvedWithEvidence.Summary.ResolvedQuestionCount);
        Assert.DoesNotContain(resolvedWithEvidence.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Open question disappeared", StringComparison.Ordinal));
        Assert.Contains(resolvedWithEvidence.Summary.RevisionSummary, summary =>
            summary.Contains("open question", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RetiredRisksCompressOnlyWithExplicitRetirementEvidence()
    {
        var parser = new MarkdownOperationalContextParser();
        var compression = new UnderstandingCompressionService();
        var current = parser.Parse("""
            # Operational Context

            ## Active Risks

            - Context growth can hide important constraints.
            """);
        var proposedWithoutEvidence = parser.Parse("""
            # Operational Context

            ## Recent Understanding Changes

            - Compression warnings were improved.
            """);
        var proposedWithEvidence = parser.Parse("""
            # Operational Context

            ## Active Risks

            - Context growth can hide important constraints.

            ## Recent Understanding Changes

            - Retired risk: context growth can hide important constraints because retention warnings now surface it.
            """);

        var missingWithoutEvidence = compression.Compress(current, proposedWithoutEvidence);
        var retiredWithEvidence = compression.Compress(current, proposedWithEvidence);

        Assert.Contains(missingWithoutEvidence.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Active risk disappeared", StringComparison.Ordinal));
        Assert.Empty(retiredWithEvidence.Document.ActiveRisks);
        Assert.Equal(1, retiredWithEvidence.Summary.RetiredRiskCount);
        Assert.DoesNotContain(retiredWithEvidence.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Active risk disappeared", StringComparison.Ordinal));
        Assert.Contains(retiredWithEvidence.Summary.RevisionSummary, summary =>
            summary.Contains("active risk", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EditingProposalRecomputesCompressionWarnings()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Human review is mandatory before promotion.
            """);
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        var edited = await harness.ReviewService.EditAsync(harness.Repository.Id, proposal.ProposalId, "# Operational Context");

        Assert.Contains(edited.CompressionSummary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Constraint disappeared", StringComparison.Ordinal));
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

    [Fact]
    public async Task BootstrapPromotionCreatesCurrentOperationalContext()
    {
        var harness = await CreateHarnessAsync();
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        var accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, "Promote it.");

        var promoted = await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);

        Assert.Equal(OperationalContextProposalStatus.Promoted, promoted.Status);
        Assert.NotNull(promoted.Promotion.PromotedAt);
        Assert.Null(promoted.Promotion.ArchivedRelativePath);
        Assert.Equal(promoted.Promotion.PromotedContentHash, promoted.Review.ReviewedContentHash);
        Assert.Contains("## Current Mental Model", await ReadAsync(harness.Repository, ".agents/operational_context.md"));
        Assert.False(File.Exists(Path.Combine(harness.Repository.Path, ".agents", "operational_context.0001.md")));
    }

    [Fact]
    public async Task RevisionPromotionArchivesPriorCurrentContextBeforeReplacement()
    {
        var harness = await CreateHarnessAsync();
        var originalContext = """
            # Operational Context

            ## Architecture

            - Prior architecture.
            """;
        await WriteAsync(harness.Repository, ".agents/operational_context.md", originalContext);
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        await harness.ReviewService.EditAsync(harness.Repository.Id, proposal.ProposalId, """
            # Operational Context

            ## Architecture

            - Replacement architecture.
            """);
        var accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);

        var promoted = await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);

        Assert.Equal(".agents/operational_context.0001.md", promoted.Promotion.ArchivedRelativePath);
        Assert.Equal(1, promoted.Promotion.RevisionNumber);
        Assert.Equal(originalContext, await ReadAsync(harness.Repository, ".agents/operational_context.0001.md"));
        Assert.Contains("Replacement architecture.", await ReadAsync(harness.Repository, ".agents/operational_context.md"));
    }

    [Fact]
    public async Task OperationalContextRotationUsesHighestExistingHistoricalNumber()
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = CreateGitRepositoryDirectory()
        };
        await WriteAsync(repository, ".agents/operational_context.md", "current");
        await WriteAsync(repository, ".agents/operational_context.0004.md", "old");
        var rotationService = new ArtifactRotationService(
            new FileSystemArtifactStore(),
            new ArtifactService(new FileSystemArtifactStore()));

        var archived = await rotationService.RotateCurrentOperationalContextAsync(repository);

        Assert.Equal(".agents/operational_context.0005.md", archived.RelativePath);
        Assert.Equal("current", await ReadAsync(repository, ".agents/operational_context.0005.md"));
    }

    [Fact]
    public async Task PromotionRejectsPendingRejectedSupersededAndStaleProposals()
    {
        var pendingHarness = await CreateHarnessAsync();
        var pending = await pendingHarness.GenerationService.GenerateAsync(pendingHarness.Repository.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pendingHarness.LifecycleService.PromoteAsync(pendingHarness.Repository.Id, pending.ProposalId));

        var rejectedHarness = await CreateHarnessAsync();
        var rejectedProposal = await rejectedHarness.GenerationService.GenerateAsync(rejectedHarness.Repository.Id);
        var rejected = await rejectedHarness.ReviewService.RejectAsync(rejectedHarness.Repository.Id, rejectedProposal.ProposalId, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rejectedHarness.LifecycleService.PromoteAsync(rejectedHarness.Repository.Id, rejected.ProposalId));

        var supersededHarness = await CreateHarnessAsync();
        var first = await supersededHarness.GenerationService.GenerateAsync(supersededHarness.Repository.Id);
        var acceptedFirst = await supersededHarness.ReviewService.AcceptAsync(supersededHarness.Repository.Id, first.ProposalId, null);
        _ = await supersededHarness.GenerationService.GenerateAsync(supersededHarness.Repository.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            supersededHarness.LifecycleService.PromoteAsync(supersededHarness.Repository.Id, acceptedFirst.ProposalId));

        var staleHarness = await CreateHarnessAsync();
        await WriteAsync(staleHarness.Repository, ".agents/operational_context.md", "# Operational Context");
        var staleProposal = await staleHarness.GenerationService.GenerateAsync(staleHarness.Repository.Id);
        var acceptedStale = await staleHarness.ReviewService.AcceptAsync(staleHarness.Repository.Id, staleProposal.ProposalId, null);
        await WriteAsync(staleHarness.Repository, ".agents/operational_context.md", "# Operational Context\n\nChanged.");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            staleHarness.LifecycleService.PromoteAsync(staleHarness.Repository.Id, acceptedStale.ProposalId));
        var reloaded = await staleHarness.ProposalStore.GetAsync(staleHarness.Repository, acceptedStale.ProposalId);
        Assert.Equal(OperationalContextReviewState.Stale, reloaded?.Review.ReviewState);
    }

    [Fact]
    public async Task ArchiveFailureBlocksPromotionAndLeavesCurrentContextUnchanged()
    {
        var artifactStore = new PathFailingArtifactStore(
            new FileSystemArtifactStore(),
            path => Path.GetFileName(path).Equals("operational_context.0001.md", StringComparison.OrdinalIgnoreCase));
        var harness = await CreateHarnessAsync(artifactStore: artifactStore);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", "current");
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        var accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId));

        Assert.Equal("current", await ReadAsync(harness.Repository, ".agents/operational_context.md"));
        var reloaded = await harness.ProposalStore.GetAsync(harness.Repository, accepted.ProposalId);
        Assert.Equal(OperationalContextProposalStatus.Accepted, reloaded?.Status);
        Assert.NotNull(reloaded?.Promotion.ArchiveFailureReason);
    }

    [Fact]
    public async Task WriteFailureDoesNotEraseCurrentContextAndReportsArchivedDuplicate()
    {
        var artifactStore = new PathFailingArtifactStore(
            new FileSystemArtifactStore(),
            path => Path.GetFileName(path).Equals("operational_context.md", StringComparison.OrdinalIgnoreCase));
        var harness = await CreateHarnessAsync(artifactStore: artifactStore);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", "current");
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        var accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId));

        Assert.Equal("current", await ReadAsync(harness.Repository, ".agents/operational_context.md"));
        Assert.Equal("current", await ReadAsync(harness.Repository, ".agents/operational_context.0001.md"));
        var reloaded = await harness.ProposalStore.GetAsync(harness.Repository, accepted.ProposalId);
        Assert.Equal(".agents/operational_context.0001.md", reloaded?.Promotion.ArchivedRelativePath);
        Assert.NotNull(reloaded?.Promotion.WriteFailureReason);
    }

    [Fact]
    public async Task ArtifactInventoryIncludesHistoricalOperationalContextRevisions()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/operational_context.md", "current");
        await WriteAsync(repository, ".agents/operational_context.0001.md", "historical");
        var projectionService = new RepositoryProjectionService(
            repositoryService,
            new ArtifactService(new FileSystemArtifactStore()),
            new PlanningService(new FileSystemArtifactStore()),
            new StaticExecutionSessionService([]),
            new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore()));

        var workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        Assert.NotNull(workspace.ArtifactInventory.OperationalContext);
        var historical = Assert.Single(workspace.ArtifactInventory.HistoricalOperationalContexts);
        Assert.Equal(".agents/operational_context.0001.md", historical.RelativePath);
    }

    [Fact]
    public async Task PromotionStateSurvivesStoreRecreation()
    {
        var harness = await CreateHarnessAsync();
        var proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        var accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);
        var promoted = await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);

        var reloadedStore = new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore());
        var reloaded = await reloadedStore.GetAsync(harness.Repository, promoted.ProposalId);

        Assert.Equal(OperationalContextProposalStatus.Promoted, reloaded?.Status);
        Assert.NotNull(reloaded?.Promotion.PromotedAt);
        Assert.Equal(promoted.Promotion.PromotedContentHash, reloaded?.Promotion.PromotedContentHash);
    }

    private static async Task<Harness> CreateHarnessAsync(
        IReadOnlyList<ExecutionSessionSummary>? executionHistory = null,
        IArtifactStore? artifactStore = null)
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        artifactStore ??= new FileSystemArtifactStore();
        var artifactService = new ArtifactService(artifactStore);
        var executionSessionService = new StaticExecutionSessionService(executionHistory ?? []);
        var proposalStore = new FileSystemOperationalContextProposalStore(artifactStore);
        var parser = new MarkdownOperationalContextParser();
        var compressionService = new UnderstandingCompressionService();
        var generationService = new OperationalContextGenerationService(
            repositoryService,
            artifactService,
            new PlanningService(artifactStore),
            executionSessionService,
            parser,
            new UnderstandingDiffService(),
            compressionService,
            proposalStore);
        var reviewService = new OperationalContextReviewService(
            repositoryService,
            artifactService,
            parser,
            new UnderstandingDiffService(),
            compressionService,
            proposalStore);
        var lifecycleService = new OperationalContextLifecycleService(
            repositoryService,
            artifactService,
            new ArtifactRotationService(artifactStore, artifactService),
            proposalStore);

        return new Harness(
            repository,
            repositoryService,
            executionSessionService,
            proposalStore,
            generationService,
            reviewService,
            lifecycleService);
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
        OperationalContextReviewService ReviewService,
        OperationalContextLifecycleService LifecycleService);

    private sealed class PathFailingArtifactStore(
        IArtifactStore innerStore,
        Func<string, bool> shouldFailWrite) : IArtifactStore
    {
        public Task<bool> ExistsAsync(string path)
        {
            return innerStore.ExistsAsync(path);
        }

        public Task<string?> ReadAsync(string path)
        {
            return innerStore.ReadAsync(path);
        }

        public Task WriteAsync(string path, string content)
        {
            if (shouldFailWrite(path))
            {
                throw new IOException($"Configured write failure for {Path.GetFileName(path)}.");
            }

            return innerStore.WriteAsync(path, content);
        }

        public Task DeleteAsync(string path)
        {
            return innerStore.DeleteAsync(path);
        }

        public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern)
        {
            return innerStore.ListAsync(path, searchPattern);
        }

        public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path)
        {
            return innerStore.ListDirectoriesAsync(path);
        }
    }

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
