using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Configuration;
using CommandCenter.Continuity;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Continuity.Services;
using CommandCenter.Execution;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Projections;
using CommandCenter.Core.Repositories;
using CommandCenter.Execution.Abstractions;
using CommandCenter.Execution.Models;
using CommandCenter.Execution.Primitives;
using CommandCenter.Middle.Continuity;
using CommandCenter.Middle.Projections;

namespace CommandCenter.Backend.Tests;

public sealed class OperationalContextGenerationTests
{
    [Fact]
    public void ParserMapsCanonicalSections()
    {
        var parser = new MarkdownOperationalContextParser();

        OperationalContextDocument document = parser.Parse("""
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

        OperationalContextDocument document = parser.Parse("""
                                                           # Operational Context

                                                           ## Architecture

                                                           - Backend services own workflow authority.

                                                           ## Hand Written Notes

                                                           Preserve this note exactly enough for reviewer inspection.
                                                           """);

        OperationalContextSection additionalSection = Assert.Single(document.AdditionalSections);
        Assert.Equal("Hand Written Notes", additionalSection.Heading);
        Assert.Contains("Preserve this note", additionalSection.Content);

        string rendered = parser.Render(document);

        Assert.Contains("## Hand Written Notes", rendered);
        Assert.Contains("Preserve this note exactly enough for reviewer inspection.", rendered);
    }

    [Fact]
    public void RendererEmitsStableCanonicalSectionOrder()
    {
        var parser = new MarkdownOperationalContextParser();

        string rendered = parser.Render(new OperationalContextDocument
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
        OperationalContextDocument current = parser.Parse("""
                                                          # Operational Context

                                                          ## Constraints

                                                          - Existing constraint.
                                                          """);
        OperationalContextDocument proposed = parser.Parse("""
                                                           # Operational Context

                                                           ## Constraints

                                                           - Existing constraint.
                                                           - New constraint.
                                                           """);

        IReadOnlyList<OperationalContextSemanticChange> changes = diff.Compare(current, proposed);

        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.ConstraintAdded &&
            change.Description.Contains("New constraint", StringComparison.Ordinal));
    }

    [Fact]
    public void DiffReportsDecisionSpecificChanges()
    {
        var parser = new MarkdownOperationalContextParser();
        var diff = new UnderstandingDiffService();
        OperationalContextDocument current = parser.Parse("""
                                                          # Operational Context

                                                          ## Stable Decisions

                                                          - Decision: Retired durable decision.

                                                          ## Decision Rationale

                                                          - Rationale for `Existing decision`: old reason.
                                                          - Rationale for `Dropped decision`: important reason.

                                                          ## Open Questions

                                                          - Open decision: Should stale proposals be auto-rejected?
                                                          """);
        OperationalContextDocument proposed = parser.Parse("""
                                                           # Operational Context

                                                           ## Stable Decisions

                                                           - Decision: New durable decision.

                                                           ## Decision Rationale

                                                           - Rationale for `Existing decision`: new reason.

                                                           ## Open Questions

                                                           - Open decision: Should diagnostics show decision retention?
                                                           """);

        IReadOnlyList<OperationalContextSemanticChange> changes = diff.Compare(current, proposed);

        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.ImportantDecisionIntroduced &&
            change.Description.Contains("New durable decision", StringComparison.Ordinal));
        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.DecisionRetired &&
            change.Description.Contains("Retired durable decision", StringComparison.Ordinal));
        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.RationaleChanged &&
            change.Description.Contains("Existing decision", StringComparison.Ordinal));
        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.RationaleLostWarning &&
            change.Description.Contains("Dropped decision", StringComparison.Ordinal));
        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.OpenDecisionPreserved &&
            change.Description.Contains("decision retention", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.OpenDecisionResolved &&
            change.Description.Contains("auto-rejected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerationSucceedsWithoutExistingOperationalContext()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m2.md", "# M2");
        await WriteAsync(harness.Repository, ".agents/handoffs/handoff.md", """
            # Handoff

            - Proposal persistence was added.
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Equal(OperationalContextProposalStatus.Pending, proposal.Status);
        Assert.NotNull(proposal.GeneratedContent);
        Assert.Contains("## Current Mental Model", proposal.GeneratedContent);
        Assert.Contains("Latest handoff signal: Proposal persistence was added.", proposal.GeneratedContent);
        Assert.False(File.Exists(Path.Combine(harness.Repository.Path, ".agents", "operational_context.md")));
    }

    [Fact]
    public async Task GenerationUsesExistingContextAndPreservesUnknownSections()
    {
        Harness harness = await CreateHarnessAsync();
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

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Contains("Existing architecture survives.", proposal.GeneratedContent);
        Assert.Contains("Proposal infrastructure has priority over generation quality.", proposal.GeneratedContent);
        Assert.Contains("## Hand Written Notes", proposal.GeneratedContent);
        Assert.Contains("Keep this local note.", proposal.GeneratedContent);
    }

    [Fact]
    public async Task ArchitecturalDecisionAndRationaleAreAssimilated()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", """
            # Decisions

            - Backend service boundaries must own workflow authority because client state cannot be authoritative.
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Contains("Decision: Backend service boundaries must own workflow authority", proposal.GeneratedContent);
        Assert.Contains("Rationale for `Backend service boundaries must own workflow authority because client state cannot be authoritative.`: client state cannot be authoritative", proposal.GeneratedContent);
        Assert.Contains("Backend service boundaries must own workflow authority because client state cannot be authoritative.", proposal.GeneratedContent);
    }

    [Fact]
    public async Task StrategicDecisionSurvivesWhileRelevant()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", """
            # Decisions

            - Reviewable deterministic classification should remain the default for future continuity work because automatic semantic authority is premature.
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Contains("Decision: Reviewable deterministic classification should remain the default for future continuity work", proposal.GeneratedContent);
        Assert.Contains("automatic semantic authority is premature", proposal.GeneratedContent);
        Assert.Contains(proposal.SemanticChanges, change =>
            change.Type == OperationalContextSemanticChangeType.ImportantDecisionIntroduced);
        Assert.Contains(proposal.SemanticChanges, change =>
            change.Type == OperationalContextSemanticChangeType.RationaleChanged);
    }

    [Fact]
    public async Task GenerationProjectsDecisionAssimilationReasons()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", """
            # Decisions

            - Backend workflow service boundaries must own operational context authority because client state cannot be authoritative.
            - M7 build passed.
            - Deprecated operational context shortcut is retired.
            """);
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.0001.md", """
            # Decisions

            - Historical backend service boundary must own old workflow authority.
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        DecisionAssimilationRecord assimilated = Assert.Single(
            proposal.DecisionAssimilation.Decisions,
            decision => decision.Statement.StartsWith("Backend workflow service boundaries", StringComparison.Ordinal));
        Assert.Equal(DecisionAssimilationStatus.Assimilated, assimilated.Status);
        Assert.True(assimilated.IsDurable);
        Assert.True(assimilated.QualifiesForAssimilation);
        Assert.True(assimilated.IsAssimilated);
        Assert.Equal("Decision: Backend workflow service boundaries must own operational context authority because client state cannot be authoritative.", assimilated.OperationalStatement);
        Assert.Equal(DecisionTaxonomy.ArchitecturalDecision, assimilated.TaxonomyBasis.Taxonomy);
        Assert.Contains("architectural-continuity-keywords", assimilated.TaxonomyBasis.MatchedRules);
        Assert.Contains(assimilated.TaxonomyBasis.MatchedEvidence, evidence =>
            evidence.Contains("backend", StringComparison.OrdinalIgnoreCase));
        Assert.False(assimilated.TaxonomyBasis.IsHeuristicFallback);
        Assert.Contains(assimilated.SourceEvidence, evidence =>
            evidence.Contains(".agents/decisions/decisions.md", StringComparison.OrdinalIgnoreCase));

        DecisionAssimilationRecord tactical = Assert.Single(
            proposal.DecisionAssimilation.Decisions,
            decision => decision.Statement == "M7 build passed.");
        Assert.Equal(DecisionAssimilationStatus.Excluded, tactical.Status);
        Assert.Contains("tactical-execution-keywords", tactical.TaxonomyBasis.MatchedRules);
        Assert.Contains(tactical.TaxonomyBasis.MatchedEvidence, evidence =>
            evidence.Contains("build", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Tactical decision", tactical.ExclusionReason!, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(proposal.DecisionAssimilation.Decisions, decision =>
            decision.Statement == "M7 build passed." &&
            decision.Status == DecisionAssimilationStatus.Excluded &&
            decision.ExclusionReason!.Contains("Tactical decision", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.DecisionAssimilation.Decisions, decision =>
            decision.Statement == "Deprecated operational context shortcut is retired." &&
            decision.Status == DecisionAssimilationStatus.Excluded &&
            decision.ExclusionReason!.Contains("superseded or retired", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposal.DecisionAssimilation.Decisions, decision =>
            decision.Statement == "Historical backend service boundary must own old workflow authority." &&
            decision.Status == DecisionAssimilationStatus.Excluded &&
            decision.TaxonomyBasis.MatchedRules.Contains("historical-artifact") &&
            decision.ExclusionReason!.Contains("Historical decision", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GenerationProjectsTaxonomyFallbackAndAmbiguityBasis()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", """
            # Decisions

            - Preserve notes.
            - Backend build verification must remain service owned.
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        DecisionAssimilationRecord fallback = Assert.Single(
            proposal.DecisionAssimilation.Decisions,
            decision => decision.Statement == "Preserve notes.");
        Assert.Equal(DecisionTaxonomy.TacticalDecision, fallback.TaxonomyBasis.Taxonomy);
        Assert.True(fallback.TaxonomyBasis.IsHeuristicFallback);
        Assert.Equal("No taxonomy rules matched; defaulted to tactical so unclassified text does not become durable operational context.", fallback.TaxonomyBasis.FallbackReason);
        Assert.Contains(fallback.TaxonomyBasis.Diagnostics, diagnostic =>
            diagnostic.Contains("No taxonomy keyword evidence", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(DecisionAssimilationStatus.Excluded, fallback.Status);

        DecisionAssimilationRecord ambiguous = Assert.Single(
            proposal.DecisionAssimilation.Decisions,
            decision => decision.Statement == "Backend build verification must remain service owned.");
        Assert.Equal(DecisionTaxonomy.ArchitecturalDecision, ambiguous.TaxonomyBasis.Taxonomy);
        Assert.Contains("architectural-continuity-keywords", ambiguous.TaxonomyBasis.MatchedRules);
        Assert.Contains("tactical-execution-keywords", ambiguous.TaxonomyBasis.MatchedRules);
        Assert.Contains("strategic-policy-keywords", ambiguous.TaxonomyBasis.MatchedRules);
        Assert.Contains(ambiguous.TaxonomyBasis.Diagnostics, diagnostic =>
            diagnostic.Contains("Ambiguous taxonomy match", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(DecisionAssimilationStatus.Assimilated, ambiguous.Status);
    }

    [Fact]
    public async Task GenerationProjectsDecisionAssimilationLimitAndOmittedItems()
    {
        Harness harness = await CreateHarnessAsync();
        string decisions = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 9).Select(index =>
                $"- Backend service boundary {index} must own workflow authority because UI derivation would drift."));
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", $"""
            # Decisions

            {decisions}
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Equal(8, proposal.DecisionAssimilation.Limit.Limit);
        Assert.Equal(9, proposal.DecisionAssimilation.Limit.TotalAnalyzedItemCount);
        Assert.Equal(9, proposal.DecisionAssimilation.Limit.TotalQualifyingItemCount);
        Assert.Equal(8, proposal.DecisionAssimilation.Limit.AssimilatedItemCount);
        Assert.Equal(1, proposal.DecisionAssimilation.Limit.OmittedItemCount);
        Assert.Equal(8, proposal.DecisionAssimilation.Decisions.Count(decision => decision.Status == DecisionAssimilationStatus.Assimilated));

        DecisionAssimilationRecord omitted = Assert.Single(
            proposal.DecisionAssimilation.Decisions,
            decision => decision.Status == DecisionAssimilationStatus.OmittedByLimit);
        Assert.True(omitted.QualifiesForAssimilation);
        Assert.True(omitted.IsOmittedByLimit);
        Assert.False(omitted.IsAssimilated);
        Assert.Null(omitted.ExclusionReason);
        Assert.Equal(proposal.DecisionAssimilation.Limit.Reason, omitted.OmissionReason);
        Assert.Equal(DecisionTaxonomy.ArchitecturalDecision, omitted.TaxonomyBasis.Taxonomy);
        Assert.Contains("architectural-continuity-keywords", omitted.TaxonomyBasis.MatchedRules);
        Assert.Contains(omitted.TaxonomyBasis.MatchedEvidence, evidence =>
            evidence.Contains("workflow authority", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(omitted.OperationalStatement!, proposal.GeneratedContent);
    }

    [Fact]
    public async Task TacticalDecisionsDoNotBloatOperationalContext()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", """
            # Decisions

            - M5 build passed.
            - Stage and commit the current slice.
            - Temporary workaround was approved for this slice.
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.DoesNotContain("M5 build passed.", proposal.GeneratedContent);
        Assert.DoesNotContain("Stage and commit the current slice.", proposal.GeneratedContent);
        Assert.Contains(proposal.CompressionSummary.Warnings, warning =>
            warning.Contains("tactical decision", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpenDecisionAppearsAsOpenQuestion()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", """
            # Decisions

            - Should operational context diagnostics include decision retention trends?
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Contains("Open decision: Should operational context diagnostics include decision retention trends?", proposal.GeneratedContent);
    }

    [Fact]
    public async Task ContradictoryDurableDecisionsAreFlagged()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", """
            # Decisions

            - Operational context generation must mutate current context.
            - Operational context generation must not mutate current context.
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        Assert.Contains(proposal.CompressionSummary.Warnings, warning =>
            warning.Contains("Contradictory decision signals", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CompressionPreservesDurableUnderstandingAndReportsTiers()
    {
        Harness harness = await CreateHarnessAsync();
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

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

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
        OperationalContextDocument current = parser.Parse("""
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
        OperationalContextDocument proposed = parser.Parse("# Operational Context");

        OperationalContextCompressionResult result = compression.Compress(current, proposed);

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
        Harness harness = await CreateHarnessAsync();
        string recentChanges = string.Join(
            Environment.NewLine,
            Enumerable.Range(1, 20).Select(index => $"- Recent execution for `.agents/milestones/m{index}.md` is recorded with state `Completed`."));
        await WriteAsync(harness.Repository, ".agents/operational_context.md", $"""
            # Operational Context

            ## Recent Understanding Changes

            {recentChanges}
            """);

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        OperationalContextDocument generated = new MarkdownOperationalContextParser().Parse(proposal.GeneratedContent ?? string.Empty);

        Assert.True(generated.RecentUnderstandingChanges.Count <= 12);
        Assert.True(proposal.CompressionSummary.CompressedItemCount > 0);
        Assert.NotEmpty(proposal.CompressionSummary.NoiseRemovedIndicators);
    }

    [Fact]
    public async Task DurableUnderstandingSurvivesRepeatedGeneratedRevisions()
    {
        Harness harness = await CreateHarnessAsync();
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

        for (int index = 0; index < 3; index++)
        {
            await WriteAsync(harness.Repository, ".agents/handoffs/handoff.md", $"""
                # Handoff

                - Slice {index} completed without changing authority boundaries.
                - Recent execution for `.agents/milestones/m5.md` is recorded with state `Completed`.
                """);

            OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
            OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);
            await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);
        }

        OperationalContextDocument current = new MarkdownOperationalContextParser().Parse(
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
    public async Task LongHorizonCertificationPreservesBoundedReviewableUnderstandingAcrossCyclesAndRestart()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", """
            # Plan

            Preserve project understanding through repository-owned artifacts.
            """);
        await WriteAsync(harness.Repository, ".agents/milestones/m8-long-horizon-certification.md", """
            # M8

            Certify repeated operational-context update cycles.
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Current Mental Model

            - Command Center treats repository `.agents` files as continuity authority.

            ## Architecture

            - Backend continuity services generate proposals while lifecycle services promote accepted content.

            ## Authority Boundaries

            - Execution sessions are disposable and cannot become project memory.

            ## Constraints

            - Operational context changes require human review before promotion.

            ## Stable Decisions

            - Repository artifacts remain authoritative across restarts.

            ## Decision Rationale

            - Rationale for `Repository artifacts remain authoritative across restarts.`: artifact state survives provider replacement and process lifetime.

            ## Open Questions

            - Should completed proposals remain pending after promotion?
            - Should dashboard diagnostics include continuity trends?

            ## Active Risks

            - Provider output may be mistaken for project memory.
            - Context growth can hide important constraints.
            """);

        int previousLength = (await ReadAsync(harness.Repository, ".agents/operational_context.md")).Length;
        OperationalContextProposal? latestProposal = null;
        for (int cycle = 1; cycle <= 3; cycle++)
        {
            await WriteAsync(harness.Repository, ".agents/handoffs/handoff.md", BuildCycleHandoff(cycle));
            await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", $"""
                # Decisions

                - Cycle {cycle} keeps backend continuity services artifact-mediated because process memory is not authoritative.
                - M8 cycle {cycle} build passed.
                """);
            harness.ExecutionSessionService.SetHistory(
            [
                new ExecutionSessionSummary
                {
                    SessionId = Guid.NewGuid(),
                    State = ExecutionSessionState.Completed,
                    RepositoryState = RepositoryExecutionState.Ready,
                    MilestonePath = ".agents/milestones/m8-long-horizon-certification.md",
                    StartedAt = DateTimeOffset.UtcNow.AddMinutes(-cycle),
                    CompletedAt = DateTimeOffset.UtcNow,
                    ProviderName = "fake"
                }
            ]);

            OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
            Assert.Contains(proposal.SemanticChanges, change =>
                change.Description.Contains($"Cycle {cycle}", StringComparison.OrdinalIgnoreCase));

            OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(
                harness.Repository.Id,
                proposal.ProposalId,
                $"Cycle {cycle} review accepted.");
            latestProposal = await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);

            string currentContent = await ReadAsync(harness.Repository, ".agents/operational_context.md");
            OperationalContextDocument current = new MarkdownOperationalContextParser().Parse(currentContent);

            Assert.Contains(current.Architecture, item =>
                item.Text.Contains("Backend continuity services generate proposals", StringComparison.Ordinal));
            Assert.Contains(current.Constraints, item =>
                item.Text.Contains("human review", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(current.StableDecisions, item =>
                item.Text.Contains("Repository artifacts remain authoritative", StringComparison.Ordinal) ||
                item.Text.Contains("artifact-mediated", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(current.DecisionRationale, item =>
                item.Text.Contains("process memory is not authoritative", StringComparison.OrdinalIgnoreCase) ||
                item.Text.Contains("survives provider replacement", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(current.OpenQuestions, item =>
                item.Text.Contains("continuity trends", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(current.ActiveRisks, item =>
                item.Text.Contains("Context growth", StringComparison.Ordinal));
            if (cycle >= 2)
            {
                Assert.DoesNotContain(current.OpenQuestions, item =>
                    item.Text.Contains("completed proposals remain pending", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(current.ActiveRisks, item =>
                    item.Text.Contains("Provider output may be mistaken", StringComparison.OrdinalIgnoreCase));
            }

            Assert.True(current.RecentUnderstandingChanges.Count <= 12);
            Assert.True(currentContent.Length <= previousLength + 2000);
            previousLength = currentContent.Length;
        }

        Assert.NotNull(latestProposal);
        Harness restarted = await RecreateHarnessAsync(harness);
        OperationalContextProposal? reloadedProposal = await restarted.ProposalStore.GetAsync(
            restarted.Repository,
            latestProposal.ProposalId,
            includeContent: true);
        RepositoryProjectionService projectionService = CreateProjectionService(restarted);
        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(restarted.Repository.Id);

        Assert.Equal(OperationalContextProposalStatus.Promoted, reloadedProposal?.Status);
        Assert.NotNull(reloadedProposal?.Promotion.PromotedAt);
        Assert.True(Directory.GetFiles(restarted.Repository.Path, "operational_context.*.md", SearchOption.AllDirectories).Length >= 3);
        Assert.True(workspace.OperationalContext.Exists);
        Assert.Equal(4, workspace.OperationalContext.CurrentRevisionNumber);
        Assert.Contains(workspace.OperationalContext.StableDecisions, item =>
            item.Text.Contains("artifact-mediated", StringComparison.OrdinalIgnoreCase) ||
            item.Text.Contains("Repository artifacts remain authoritative", StringComparison.Ordinal));
        Assert.Contains(workspace.OperationalContext.OpenQuestions, item =>
            item.Text.Contains("continuity trends", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(workspace.OperationalContext.ActiveRisks, item =>
            item.Text.Contains("Context growth", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FreshParticipantCanReconstructMentalModelWithoutHistoricalArchives()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", """
            # Plan

            Command Center preserves current understanding in operational context.
            """);
        await WriteAsync(harness.Repository, ".agents/milestones/m8-long-horizon-certification.md", """
            # M8

            Certify archive-independent orientation.
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.0001.md", """
            # Operational Context

            ## Architecture

            - Obsolete historical architecture that must not be required for orientation.
            """);
        await WriteAsync(harness.Repository, ".agents/handoffs/handoff.0001.md", "# Old Handoff\n\n- Historical detail.");
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.0001.md", "# Old Decisions\n\n- Historical detail.");
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Current Mental Model

            - Current understanding is reconstructed from plan, selected milestone, and operational context.

            ## Architecture

            - Backend projections expose understanding from repository artifacts.

            ## Constraints

            - Review is mandatory before current understanding changes.

            ## Stable Decisions

            - Operational context is the authoritative current understanding artifact.

            ## Decision Rationale

            - Rationale for `Operational context is the authoritative current understanding artifact.`: current context avoids replaying archives for orientation.

            ## Open Questions

            - Which long-horizon diagnostics should be reported first?

            ## Active Risks

            - Drift could hide a missing constraint.
            """);

        string plan = await ReadAsync(harness.Repository, ".agents/plan.md");
        string milestone = await ReadAsync(harness.Repository, ".agents/milestones/m8-long-horizon-certification.md");
        string currentContext = await ReadAsync(harness.Repository, ".agents/operational_context.md");
        string reconstructionInput = $"{plan}\n{milestone}\n{currentContext}";

        Assert.Contains("Backend projections expose understanding", reconstructionInput);
        Assert.Contains("Review is mandatory", reconstructionInput);
        Assert.Contains("authoritative current understanding", reconstructionInput);
        Assert.Contains("avoids replaying archives", reconstructionInput);
        Assert.Contains("long-horizon diagnostics", reconstructionInput);
        Assert.Contains("Drift could hide", reconstructionInput);
        Assert.DoesNotContain("Obsolete historical architecture", reconstructionInput);
        Assert.DoesNotContain("Historical detail", reconstructionInput);
    }

    [Fact]
    public void DriftDetectionFlagsStableUnderstandingLossWithoutInputEvidence()
    {
        var parser = new MarkdownOperationalContextParser();
        var compression = new UnderstandingCompressionService();
        OperationalContextDocument current = parser.Parse("""
                                                          # Operational Context

                                                          ## Architecture

                                                          - Backend services own operational-context promotion.

                                                          ## Constraints

                                                          - Promotion requires accepted review metadata.

                                                          ## Stable Decisions

                                                          - Proposal artifacts survive process restart.

                                                          ## Decision Rationale

                                                          - Rationale for `Proposal artifacts survive process restart.`: proposal metadata is repository-owned.

                                                          ## Open Questions

                                                          - Should reports include drift trends?
                                                          """);
        OperationalContextDocument proposed = parser.Parse("""
                                                           # Operational Context

                                                           ## Stable Decisions

                                                           - Proposal artifacts survive process restart.
                                                           """);

        OperationalContextCompressionResult result = compression.Compress(current, proposed);

        Assert.Contains(result.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Architecture disappeared", StringComparison.Ordinal));
        Assert.Contains(result.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Constraint disappeared", StringComparison.Ordinal));
        Assert.Contains(result.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Open question disappeared", StringComparison.Ordinal));
        Assert.Contains(result.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Decision rationale disappeared", StringComparison.Ordinal));
    }

    [Fact]
    public async Task WorkspaceAndDashboardRemainScannableAfterMultipleRevisions()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Current Mental Model

            - The workspace shows current understanding without client-side authority.

            ## Stable Decisions

            - Backend projections are the continuity read model.

            ## Open Questions

            - Should dashboard continuity counts include stale proposals?

            ## Active Risks

            - Review warnings could be missed if summaries grow too large.
            """);

        for (int cycle = 1; cycle <= 3; cycle++)
        {
            await WriteAsync(harness.Repository, ".agents/handoffs/handoff.md", $"""
                # Handoff

                - Workspace certification cycle {cycle} preserved the existing read model.
                """);
            OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
            OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);
            await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);
        }

        RepositoryProjectionService projectionService = CreateProjectionService(harness);
        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(harness.Repository.Id);
        RepositoryDashboardProjection dashboard = Assert.Single(await projectionService.GetDashboardAsync());

        Assert.True(workspace.OperationalContext.CurrentUnderstandingSummary.Count <= 3);
        Assert.NotEmpty(workspace.OperationalContext.StableDecisions);
        Assert.NotEmpty(workspace.OperationalContext.OpenQuestions);
        Assert.NotEmpty(workspace.OperationalContext.ActiveRisks);
        Assert.NotEmpty(workspace.OperationalContext.RecentUnderstandingChanges);
        Assert.Equal(4, dashboard.ContinuitySummary.OperationalContextRevisionCount);
        Assert.Equal(workspace.OperationalContext.OpenQuestions.Count, dashboard.ContinuitySummary.OpenQuestionCount);
        Assert.Equal(workspace.OperationalContext.ActiveRisks.Count, dashboard.ContinuitySummary.ActiveRiskCount);
    }

    [Fact]
    public void ResolvedQuestionsCompressOnlyWithExplicitResolutionEvidence()
    {
        var parser = new MarkdownOperationalContextParser();
        var compression = new UnderstandingCompressionService();
        OperationalContextDocument current = parser.Parse("""
                                                          # Operational Context

                                                          ## Open Questions

                                                          - Should diagnostics include growth trends?
                                                          """);
        OperationalContextDocument proposedWithoutEvidence = parser.Parse("""
                                                                          # Operational Context

                                                                          ## Recent Understanding Changes

                                                                          - Diagnostics were discussed.
                                                                          """);
        OperationalContextDocument proposedWithEvidence = parser.Parse("""
                                                                       # Operational Context

                                                                       ## Open Questions

                                                                       - Should diagnostics include growth trends?

                                                                       ## Recent Understanding Changes

                                                                       - Resolved question: diagnostics include growth trends.
                                                                       """);

        OperationalContextCompressionResult missingWithoutEvidence = compression.Compress(current, proposedWithoutEvidence);
        OperationalContextCompressionResult resolvedWithEvidence = compression.Compress(current, proposedWithEvidence);

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
        OperationalContextDocument current = parser.Parse("""
                                                          # Operational Context

                                                          ## Active Risks

                                                          - Context growth can hide important constraints.
                                                          """);
        OperationalContextDocument proposedWithoutEvidence = parser.Parse("""
                                                                          # Operational Context

                                                                          ## Recent Understanding Changes

                                                                          - Compression warnings were improved.
                                                                          """);
        OperationalContextDocument proposedWithEvidence = parser.Parse("""
                                                                       # Operational Context

                                                                       ## Active Risks

                                                                       - Context growth can hide important constraints.

                                                                       ## Recent Understanding Changes

                                                                       - Retired risk: context growth can hide important constraints because retention warnings now surface it.
                                                                       """);

        OperationalContextCompressionResult missingWithoutEvidence = compression.Compress(current, proposedWithoutEvidence);
        OperationalContextCompressionResult retiredWithEvidence = compression.Compress(current, proposedWithEvidence);

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
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Human review is mandatory before promotion.
            """);
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        OperationalContextProposal edited = await harness.ReviewService.EditAsync(harness.Repository.Id, proposal.ProposalId, "# Operational Context");

        Assert.Contains(edited.CompressionSummary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Constraint disappeared", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProposalPersistsAcrossStoreRecreation()
    {
        Harness harness = await CreateHarnessAsync();
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        var reloadedStore = new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore());
        OperationalContextProposal? reloaded = await reloadedStore.GetAsync(harness.Repository, proposal.ProposalId, includeContent: true);

        Assert.NotNull(reloaded);
        Assert.Equal(proposal.ProposalId, reloaded.ProposalId);
        Assert.Equal(proposal.GeneratedContentHash, reloaded.GeneratedContentHash);
        Assert.Contains("## Current Mental Model", reloaded.GeneratedContent);
    }

    [Fact]
    public async Task ProposalListingSkipsCorruptMetadataArtifacts()
    {
        Harness harness = await CreateHarnessAsync();
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        await WriteAsync(
            harness.Repository,
            ".agents/operational_context/proposals/corrupt-proposal/metadata.json",
            "{ not valid json");

        IReadOnlyList<OperationalContextProposal> proposals = await harness.ProposalStore.ListAsync(harness.Repository);

        Assert.Single(proposals);
        Assert.Equal(proposal.ProposalId, proposals[0].ProposalId);
    }

    [Fact]
    public async Task RegenerationSupersedesPreviousPendingProposal()
    {
        Harness harness = await CreateHarnessAsync();
        OperationalContextProposal first = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        OperationalContextProposal second = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        IReadOnlyList<OperationalContextProposal> proposals = await harness.ProposalStore.ListAsync(harness.Repository);

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
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/plan.md", "plan");
        await WriteAsync(harness.Repository, ".agents/milestones/m2.md", "# M2");
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        var projectionService = new RepositoryProjectionService(
            harness.RepositoryService,
            new ArtifactService(new FileSystemArtifactStore()),
            new PlanningService(new FileSystemArtifactStore()),
            harness.ExecutionSessionService,
            harness.ProposalStore,
            new MarkdownOperationalContextParser(),
            new FileSystemArtifactStore());

        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(harness.Repository.Id);

        Assert.True(workspace.OperationalContextProposalSummary.PendingProposalExists);
        Assert.Equal(proposal.ProposalId, workspace.OperationalContextProposalSummary.LatestProposalId);
        Assert.Equal(OperationalContextProposalStatus.Pending, workspace.OperationalContextProposalSummary.Status);
        Assert.True(workspace.OperationalContextProposalSummary.SourceInputCount > 0);
        Assert.True(workspace.OperationalContextProposalSummary.ContentByteCount > 0);
    }

    [Fact]
    public async Task PendingProposalIsReviewableAndLoadsContent()
    {
        Harness harness = await CreateHarnessAsync();

        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        OperationalContextProposal? loaded = await harness.ProposalStore.GetAsync(harness.Repository, proposal.ProposalId, includeContent: true);

        Assert.NotNull(loaded);
        Assert.Equal(OperationalContextReviewState.PendingReview, loaded.Review.ReviewState);
        Assert.NotNull(loaded.GeneratedContent);
        Assert.Null(loaded.EditedContent);
    }

    [Fact]
    public async Task EditingPersistsReviewerContentAndRecomputesSemanticChanges()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Existing constraint.
            """);
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        OperationalContextProposal edited = await harness.ReviewService.EditAsync(harness.Repository.Id, proposal.ProposalId, """
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
        Harness harness = await CreateHarnessAsync();
        string currentContext = """
                                # Operational Context

                                ## Architecture

                                - Existing architecture.
                                """;
        await WriteAsync(harness.Repository, ".agents/operational_context.md", currentContext);
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, "Looks right.");

        Assert.Equal(OperationalContextProposalStatus.Accepted, accepted.Status);
        Assert.Equal(OperationalContextReviewState.Accepted, accepted.Review.ReviewState);
        Assert.Equal("Looks right.", accepted.Review.ReviewNote);
        Assert.Equal(proposal.GeneratedContentHash, accepted.Review.ReviewedContentHash);
        Assert.Equal(currentContext, await ReadAsync(harness.Repository, ".agents/operational_context.md"));
    }

    [Fact]
    public async Task RejectRecordsReviewStateAndLeavesContentForAudit()
    {
        Harness harness = await CreateHarnessAsync();
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);

        OperationalContextProposal rejected = await harness.ReviewService.RejectAsync(harness.Repository.Id, proposal.ProposalId, "Not enough signal.");

        Assert.Equal(OperationalContextProposalStatus.Rejected, rejected.Status);
        Assert.Equal(OperationalContextReviewState.Rejected, rejected.Review.ReviewState);
        Assert.Equal("Not enough signal.", rejected.Review.ReviewNote);
        Assert.NotNull(rejected.GeneratedContent);
    }

    [Fact]
    public async Task AcceptFailsForMissingSupersededOrStaleProposal()
    {
        Harness harness = await CreateHarnessAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            harness.ReviewService.AcceptAsync(harness.Repository.Id, "missing", null));

        OperationalContextProposal first = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        _ = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.ReviewService.AcceptAsync(harness.Repository.Id, first.ProposalId, null));

        Harness staleHarness = await CreateHarnessAsync();
        await WriteAsync(staleHarness.Repository, ".agents/operational_context.md", "# Operational Context");
        OperationalContextProposal staleProposal = await staleHarness.GenerationService.GenerateAsync(staleHarness.Repository.Id);
        await WriteAsync(staleHarness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Changed after generation.
            """);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            staleHarness.ReviewService.AcceptAsync(staleHarness.Repository.Id, staleProposal.ProposalId, null));
        OperationalContextProposal? reloaded = await staleHarness.ProposalStore.GetAsync(staleHarness.Repository, staleProposal.ProposalId);
        Assert.Equal(OperationalContextReviewState.Stale, reloaded?.Review.ReviewState);
    }

    [Fact]
    public async Task ReviewStateSurvivesStoreRecreation()
    {
        Harness harness = await CreateHarnessAsync();
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        OperationalContextProposal edited = await harness.ReviewService.EditAsync(harness.Repository.Id, proposal.ProposalId, "# Operational Context");

        var reloadedStore = new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore());
        OperationalContextProposal? reloaded = await reloadedStore.GetAsync(harness.Repository, edited.ProposalId, includeContent: true);

        Assert.NotNull(reloaded);
        Assert.Equal(OperationalContextProposalStatus.Edited, reloaded.Status);
        Assert.Equal(OperationalContextReviewState.Edited, reloaded.Review.ReviewState);
        Assert.Equal("# Operational Context", reloaded.EditedContent);
    }

    [Fact]
    public async Task BootstrapPromotionCreatesCurrentOperationalContext()
    {
        Harness harness = await CreateHarnessAsync();
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, "Promote it.");

        OperationalContextProposal promoted = await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);

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
        Harness harness = await CreateHarnessAsync();
        string originalContext = """
                                 # Operational Context

                                 ## Architecture

                                 - Prior architecture.
                                 """;
        await WriteAsync(harness.Repository, ".agents/operational_context.md", originalContext);
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        await harness.ReviewService.EditAsync(harness.Repository.Id, proposal.ProposalId, """
            # Operational Context

            ## Architecture

            - Replacement architecture.
            """);
        OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);

        OperationalContextProposal promoted = await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);

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

        Artifact archived = await rotationService.RotateCurrentOperationalContextAsync(repository);

        Assert.Equal(".agents/operational_context.0005.md", archived.RelativePath);
        Assert.Equal("current", await ReadAsync(repository, ".agents/operational_context.0005.md"));
    }

    [Fact]
    public async Task PromotionRejectsPendingRejectedSupersededAndStaleProposals()
    {
        Harness pendingHarness = await CreateHarnessAsync();
        OperationalContextProposal pending = await pendingHarness.GenerationService.GenerateAsync(pendingHarness.Repository.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            pendingHarness.LifecycleService.PromoteAsync(pendingHarness.Repository.Id, pending.ProposalId));

        Harness rejectedHarness = await CreateHarnessAsync();
        OperationalContextProposal rejectedProposal = await rejectedHarness.GenerationService.GenerateAsync(rejectedHarness.Repository.Id);
        OperationalContextProposal rejected = await rejectedHarness.ReviewService.RejectAsync(rejectedHarness.Repository.Id, rejectedProposal.ProposalId, null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            rejectedHarness.LifecycleService.PromoteAsync(rejectedHarness.Repository.Id, rejected.ProposalId));

        Harness supersededHarness = await CreateHarnessAsync();
        OperationalContextProposal first = await supersededHarness.GenerationService.GenerateAsync(supersededHarness.Repository.Id);
        OperationalContextProposal acceptedFirst = await supersededHarness.ReviewService.AcceptAsync(supersededHarness.Repository.Id, first.ProposalId, null);
        _ = await supersededHarness.GenerationService.GenerateAsync(supersededHarness.Repository.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            supersededHarness.LifecycleService.PromoteAsync(supersededHarness.Repository.Id, acceptedFirst.ProposalId));

        Harness staleHarness = await CreateHarnessAsync();
        await WriteAsync(staleHarness.Repository, ".agents/operational_context.md", "# Operational Context");
        OperationalContextProposal staleProposal = await staleHarness.GenerationService.GenerateAsync(staleHarness.Repository.Id);
        OperationalContextProposal acceptedStale = await staleHarness.ReviewService.AcceptAsync(staleHarness.Repository.Id, staleProposal.ProposalId, null);
        await WriteAsync(staleHarness.Repository, ".agents/operational_context.md", "# Operational Context\n\nChanged.");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            staleHarness.LifecycleService.PromoteAsync(staleHarness.Repository.Id, acceptedStale.ProposalId));
        OperationalContextProposal? reloaded = await staleHarness.ProposalStore.GetAsync(staleHarness.Repository, acceptedStale.ProposalId);
        Assert.Equal(OperationalContextReviewState.Stale, reloaded?.Review.ReviewState);
    }

    [Fact]
    public async Task ArchiveFailureBlocksPromotionAndLeavesCurrentContextUnchanged()
    {
        var artifactStore = new PathFailingArtifactStore(
            new FileSystemArtifactStore(),
            path => Path.GetFileName(path).Equals("operational_context.0001.md", StringComparison.OrdinalIgnoreCase));
        Harness harness = await CreateHarnessAsync(artifactStore: artifactStore);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", "current");
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId));

        Assert.Equal("current", await ReadAsync(harness.Repository, ".agents/operational_context.md"));
        OperationalContextProposal? reloaded = await harness.ProposalStore.GetAsync(harness.Repository, accepted.ProposalId);
        Assert.Equal(OperationalContextProposalStatus.Accepted, reloaded?.Status);
        Assert.NotNull(reloaded?.Promotion.ArchiveFailureReason);
    }

    [Fact]
    public async Task WriteFailureDoesNotEraseCurrentContextAndReportsArchivedDuplicate()
    {
        var artifactStore = new PathFailingArtifactStore(
            new FileSystemArtifactStore(),
            path => Path.GetFileName(path).Equals("operational_context.md", StringComparison.OrdinalIgnoreCase));
        Harness harness = await CreateHarnessAsync(artifactStore: artifactStore);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", "current");
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId));

        Assert.Equal("current", await ReadAsync(harness.Repository, ".agents/operational_context.md"));
        Assert.Equal("current", await ReadAsync(harness.Repository, ".agents/operational_context.0001.md"));
        OperationalContextProposal? reloaded = await harness.ProposalStore.GetAsync(harness.Repository, accepted.ProposalId);
        Assert.Equal(".agents/operational_context.0001.md", reloaded?.Promotion.ArchivedRelativePath);
        Assert.NotNull(reloaded?.Promotion.WriteFailureReason);
    }

    [Fact]
    public async Task ArtifactInventoryIncludesHistoricalOperationalContextRevisions()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        await WriteAsync(repository, ".agents/operational_context.md", "current");
        await WriteAsync(repository, ".agents/operational_context.0001.md", "historical");
        var projectionService = new RepositoryProjectionService(
            repositoryService,
            new ArtifactService(new FileSystemArtifactStore()),
            new PlanningService(new FileSystemArtifactStore()),
            new StaticExecutionSessionService([]),
            new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore()),
            new MarkdownOperationalContextParser(),
            new FileSystemArtifactStore());

        RepositoryWorkspaceProjection workspace = await projectionService.GetWorkspaceAsync(repository.Id);

        Assert.NotNull(workspace.ArtifactInventory.OperationalContext);
        Artifact historical = Assert.Single(workspace.ArtifactInventory.HistoricalOperationalContexts);
        Assert.Equal(".agents/operational_context.0001.md", historical.RelativePath);
    }

    [Fact]
    public async Task PromotionStateSurvivesStoreRecreation()
    {
        Harness harness = await CreateHarnessAsync();
        OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
        OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);
        OperationalContextProposal promoted = await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);

        var reloadedStore = new FileSystemOperationalContextProposalStore(new FileSystemArtifactStore());
        OperationalContextProposal? reloaded = await reloadedStore.GetAsync(harness.Repository, promoted.ProposalId);

        Assert.Equal(OperationalContextProposalStatus.Promoted, reloaded?.Status);
        Assert.NotNull(reloaded?.Promotion.PromotedAt);
        Assert.Equal(promoted.Promotion.PromotedContentHash, reloaded?.Promotion.PromotedContentHash);
    }

    [Fact]
    public async Task RepeatedProposalCyclesDoNotReplayLargeDecisionArchiveIntoOperationalContext()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.md", """
            # Decisions

            - Backend continuity services must remain artifact-mediated because hidden session memory is not authoritative.
            - M6 build passed.
            - Stage and commit the current slice.
            - Temporary workaround was approved for this slice.
            - Next slice should update the UI.
            - Verification completed for the previous run.
            """);
        await WriteAsync(harness.Repository, ".agents/decisions/decisions.0001.md", """
            # Decisions

            - Old milestone investigation completed.
            - Historical approval: run the focused backend test.
            - Completed cleanup from M4.
            """);

        for (int index = 0; index < 3; index++)
        {
            OperationalContextProposal proposal = await harness.GenerationService.GenerateAsync(harness.Repository.Id);
            OperationalContextProposal accepted = await harness.ReviewService.AcceptAsync(harness.Repository.Id, proposal.ProposalId, null);
            await harness.LifecycleService.PromoteAsync(harness.Repository.Id, accepted.ProposalId);
        }

        OperationalContextDocument current = new MarkdownOperationalContextParser().Parse(
            await ReadAsync(harness.Repository, ".agents/operational_context.md"));

        Assert.Single(current.StableDecisions);
        Assert.Contains(current.StableDecisions, item =>
            item.Text.Contains("artifact-mediated", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(current.StableDecisions, item =>
            item.Text.Contains("build passed", StringComparison.OrdinalIgnoreCase) ||
            item.Text.Contains("Stage and commit", StringComparison.OrdinalIgnoreCase) ||
            item.Text.Contains("Old milestone investigation", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Harness> CreateHarnessAsync(
        IReadOnlyList<ExecutionSessionSummary>? executionHistory = null,
        IArtifactStore? artifactStore = null)
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        string configurationPath = Path.Combine(CreateTemporaryDirectory(), "configuration.json");
        var repositoryService = new RepositoryService(new ApplicationConfigurationStore(configurationPath));
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        artifactStore ??= new FileSystemArtifactStore();
        return CreateHarness(
            repository,
            repositoryService,
            configurationPath,
            executionHistory ?? [],
            artifactStore);
    }

    private static async Task<Harness> RecreateHarnessAsync(Harness harness)
    {
        var repositoryService = new RepositoryService(new ApplicationConfigurationStore(harness.ConfigurationPath));
        Repository repository = (await repositoryService.GetAllAsync()).Single(repository => repository.Id == harness.Repository.Id);
        return CreateHarness(
            repository,
            repositoryService,
            harness.ConfigurationPath,
            [],
            new FileSystemArtifactStore());
    }

    private static Harness CreateHarness(
        Repository repository,
        RepositoryService repositoryService,
        string configurationPath,
        IReadOnlyList<ExecutionSessionSummary> executionHistory,
        IArtifactStore artifactStore)
    {
        var artifactService = new ArtifactService(artifactStore);
        var executionSessionService = new StaticExecutionSessionService(executionHistory);
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
            new DecisionAnalysisService(),
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
            configurationPath,
            repositoryService,
            executionSessionService,
            proposalStore,
            generationService,
            reviewService,
            lifecycleService);
    }

    private static RepositoryProjectionService CreateProjectionService(Harness harness)
    {
        return new RepositoryProjectionService(
            harness.RepositoryService,
            new ArtifactService(new FileSystemArtifactStore()),
            new PlanningService(new FileSystemArtifactStore()),
            harness.ExecutionSessionService,
            harness.ProposalStore,
            new MarkdownOperationalContextParser(),
            new FileSystemArtifactStore());
    }

    private static string BuildCycleHandoff(int cycle)
    {
        string outcomeEvidence = cycle switch
        {
            1 => "- Cycle 1 preserved backend workflow authority.",
            2 => """
                - Resolved question: completed proposals remain pending after promotion because promoted proposals are marked promoted.
                - Retired risk: provider output may be mistaken for project memory because proposals remain reviewable artifacts.
                """,
            _ => "- Cycle 3 confirmed repeated reviews without historical accretion."
        };

        return $"""
            # Handoff

            - Cycle {cycle} completed operational-context review and promotion.
            {outcomeEvidence}
            - Recent execution for `.agents/milestones/m8-long-horizon-certification.md` is recorded with state `Completed`.
            """;
    }

    private static async Task WriteAsync(Repository repository, string relativePath, string content)
    {
        string path = Path.Combine(repository.Path, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string? directory = Path.GetDirectoryName(path);
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
        string directory = CreateTemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory, ".git"));
        return directory;
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CommandCenter.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed record Harness(
        Repository Repository,
        string ConfigurationPath,
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

    private sealed class StaticExecutionSessionService(IReadOnlyList<ExecutionSessionSummary> initialHistory)
        : IExecutionSessionService
    {
        private IReadOnlyList<ExecutionSessionSummary> history = initialHistory;

        public void SetHistory(IReadOnlyList<ExecutionSessionSummary> nextHistory)
        {
            history = nextHistory;
        }

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
