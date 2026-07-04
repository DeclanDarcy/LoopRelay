using CommandCenter.Core.Artifacts;
using CommandCenter.Continuity;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Continuity.Services;
using CommandCenter.Core.Planning;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Continuity.Tests;

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
    public void DiffReportsPersistentItemIdModificationInsteadOfRemoveAdd()
    {
        var diff = new UnderstandingDiffService();
        OperationalContextDocument current = new()
        {
            Constraints =
            [
                new OperationalContextItem
                {
                    Id = "constraint-stable-lineage",
                    Kind = OperationalContextItemKind.Constraint,
                    Text = "Backend continuity services must own operational context review."
                }
            ]
        };
        OperationalContextDocument proposed = new()
        {
            Constraints =
            [
                new OperationalContextItem
                {
                    Id = "constraint-stable-lineage",
                    Kind = OperationalContextItemKind.Constraint,
                    Text = "Backend continuity services must own operational context review and promotion."
                }
            ]
        };

        IReadOnlyList<OperationalContextSemanticChange> changes = diff.Compare(current, proposed);

        OperationalContextSemanticChange change = Assert.Single(changes, change =>
            change.Type == OperationalContextSemanticChangeType.ModifiedConstraint);
        Assert.Equal("persistent-item-id", change.IdentityBasis);
        Assert.Equal("Backend continuity services must own operational context review.", change.PreviousState);
        Assert.Equal("Backend continuity services must own operational context review and promotion.", change.CurrentState);
        Assert.Contains("same backend item id", change.ModificationReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(change.SupportingEvidence, evidence =>
            evidence.Contains("Previous item id: constraint-stable-lineage", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(changes, change =>
            change.Type is OperationalContextSemanticChangeType.ConstraintAdded or OperationalContextSemanticChangeType.ConstraintRemoved);
    }

    [Fact]
    public void DiffReportsSourceReferenceModificationInsteadOfRemoveAdd()
    {
        var diff = new UnderstandingDiffService();
        OperationalContextDocument current = new()
        {
            StableDecisions =
            [
                new OperationalContextItem
                {
                    Id = "stable-decision-old",
                    Kind = OperationalContextItemKind.StableDecision,
                    Text = "Decision: Workflow projection owns lifecycle status.",
                    SourceRelativePath = ".agents/decisions/decisions.md"
                }
            ]
        };
        OperationalContextDocument proposed = new()
        {
            StableDecisions =
            [
                new OperationalContextItem
                {
                    Id = "stable-decision-new",
                    Kind = OperationalContextItemKind.StableDecision,
                    Text = "Decision: Workflow projection owns lifecycle status and gate evidence.",
                    SourceRelativePath = ".agents/decisions/decisions.md"
                }
            ]
        };

        IReadOnlyList<OperationalContextSemanticChange> changes = diff.Compare(current, proposed);

        OperationalContextSemanticChange change = Assert.Single(changes, change =>
            change.Type == OperationalContextSemanticChangeType.ModifiedDecision);
        Assert.Equal("source-reference", change.IdentityBasis);
        Assert.Contains(change.SupportingEvidence, evidence =>
            evidence.Contains(".agents/decisions/decisions.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(changes, change =>
            change.Type is OperationalContextSemanticChangeType.ImportantDecisionIntroduced or OperationalContextSemanticChangeType.DecisionRetired);
    }

    [Fact]
    public void DiffReportsSemanticLineageModificationInsteadOfRemoveAdd()
    {
        var parser = new MarkdownOperationalContextParser();
        var diff = new UnderstandingDiffService();
        OperationalContextDocument current = parser.Parse("""
                                                          # Operational Context

                                                          ## Constraints

                                                          - Backend workflow projection must own operational lifecycle status.
                                                          """);
        OperationalContextDocument proposed = parser.Parse("""
                                                           # Operational Context

                                                           ## Constraints

                                                           - Backend workflow projection must own operational lifecycle status, gate evidence, and recovery hints.
                                                           """);

        IReadOnlyList<OperationalContextSemanticChange> changes = diff.Compare(current, proposed);

        OperationalContextSemanticChange change = Assert.Single(changes, change =>
            change.Type == OperationalContextSemanticChangeType.ModifiedConstraint);
        Assert.Equal("section-semantic-lineage", change.IdentityBasis);
        Assert.Contains(change.SupportingEvidence, evidence =>
            evidence.Contains("Semantic lineage key", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(changes, change =>
            change.Type is OperationalContextSemanticChangeType.ConstraintAdded or OperationalContextSemanticChangeType.ConstraintRemoved);
    }

    [Fact]
    public void DiffKeepsGenuineAdditionsAndRemovalsWhenIdentityDoesNotMatch()
    {
        var parser = new MarkdownOperationalContextParser();
        var diff = new UnderstandingDiffService();
        OperationalContextDocument current = parser.Parse("""
                                                          # Operational Context

                                                          ## Constraints

                                                          - Backend workflow projection must own operational lifecycle status.
                                                          """);
        OperationalContextDocument proposed = parser.Parse("""
                                                           # Operational Context

                                                           ## Constraints

                                                           - UI affordances should render workflow gate evidence.
                                                           """);

        IReadOnlyList<OperationalContextSemanticChange> changes = diff.Compare(current, proposed);

        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.ConstraintRemoved &&
            change.Description.Contains("Backend workflow projection", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(changes, change =>
            change.Type == OperationalContextSemanticChangeType.ConstraintAdded &&
            change.Description.Contains("UI affordances", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(changes, change => change.Type == OperationalContextSemanticChangeType.ItemChanged);
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
        Assert.Contains(result.Summary.ItemOutcomes, outcome =>
            outcome.Outcome == "Removed" &&
            outcome.ItemKind == "Architecture" &&
            outcome.Rule == "retention-warning-check" &&
            outcome.Threshold.Contains("explicit resolution evidence", StringComparison.OrdinalIgnoreCase) &&
            outcome.Evidence.Any(evidence => evidence.Contains("Backend services own workflow authority", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task FreshParticipantCanReconstructMentalModelWithoutHistoricalArchives()
    {
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = CreateGitRepositoryDirectory()
        };
        await WriteAsync(repository, ".agents/plan.md", """
            # Plan

            Command Center preserves current understanding in operational context.
            """);
        await WriteAsync(repository, ".agents/milestones/m8-long-horizon-certification.md", """
            # M8

            Certify archive-independent orientation.
            """);
        await WriteAsync(repository, ".agents/operational_context.0001.md", """
            # Operational Context

            ## Architecture

            - Obsolete historical architecture that must not be required for orientation.
            """);
        await WriteAsync(repository, ".agents/handoffs/handoff.0001.md", "# Old Handoff\n\n- Historical detail.");
        await WriteAsync(repository, ".agents/decisions/decisions.0001.md", "# Old Decisions\n\n- Historical detail.");
        await WriteAsync(repository, ".agents/operational_context.md", """
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

        string plan = await ReadAsync(repository, ".agents/plan.md");
        string milestone = await ReadAsync(repository, ".agents/milestones/m8-long-horizon-certification.md");
        string currentContext = await ReadAsync(repository, ".agents/operational_context.md");
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
    public void CompressionEmitsItemOutcomeCategoriesWithRulesThresholdsAndEvidence()
    {
        var parser = new MarkdownOperationalContextParser();
        var compression = new UnderstandingCompressionService();
        OperationalContextDocument current = parser.Parse("""
                                                          # Operational Context

                                                          ## Constraints

                                                          - Human review is mandatory before promotion.

                                                          ## Stable Decisions

                                                          - Legacy workspace state remains pending.

                                                          ## Open Questions

                                                          - Should diagnostics include growth trends?

                                                          ## Active Risks

                                                          - Context growth can hide important constraints.
                                                          """);
        OperationalContextDocument proposed = parser.Parse("""
                                                           # Operational Context

                                                           ## Architecture

                                                           - Backend projections expose compression explanations.

                                                           ## Constraints

                                                           - Human review is mandatory before promotion.

                                                           ## Open Questions

                                                           - Should diagnostics include growth trends?

                                                           ## Active Risks

                                                           - Context growth can hide important constraints.

                                                           ## Recent Understanding Changes

                                                           - Old transient detail one.
                                                           - Old transient detail two.
                                                           - Repeated investigation detail.
                                                           - Repeated investigation detail.
                                                           - Recent execution for `.agents/milestones/m7.md` is recorded with state `Completed`.
                                                           - Durable recent change 1.
                                                           - Durable recent change 2.
                                                           - Durable recent change 3.
                                                           - Durable recent change 4.
                                                           - Durable recent change 5.
                                                           - Durable recent change 6.
                                                           - Durable recent change 7.
                                                           - Durable recent change 8.
                                                           - Durable recent change 9.
                                                           - Durable recent change 10.
                                                           - Resolved question: diagnostics include growth trends.
                                                           - Retired risk: context growth can hide important constraints.
                                                           """);

        OperationalContextCompressionResult result = compression.Compress(current, proposed);
        OperationalContextCompressionOutcome[] outcomes = result.Summary.ItemOutcomes.ToArray();

        AssertCompressionOutcome(outcomes, "Retained", "Constraint", "normalized-text-retention");
        AssertCompressionOutcome(outcomes, "Added", "Architecture", "proposal-addition");
        AssertCompressionOutcome(outcomes, "Removed", "StableDecision", "retention-warning-check");
        AssertCompressionOutcome(outcomes, "Compressed", "RecentChange", "recent-change-window-limit");
        AssertCompressionOutcome(outcomes, "DuplicateRemoved", "RecentChange", "recent-change-duplicate-removal");
        AssertCompressionOutcome(outcomes, "TransientRemoved", "RecentChange", "transient-execution-noise-removal");
        AssertCompressionOutcome(outcomes, "ResolvedQuestion", "OpenQuestion", "explicit-question-resolution");
        AssertCompressionOutcome(outcomes, "RetiredRisk", "ActiveRisk", "explicit-risk-retirement");
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
        Assert.Contains(resolvedWithEvidence.Summary.ItemOutcomes, outcome =>
            outcome.Outcome == "ResolvedQuestion" &&
            outcome.ItemKind == "OpenQuestion" &&
            outcome.Rule == "explicit-question-resolution" &&
            outcome.Rationale.Contains("explicitly resolved", StringComparison.OrdinalIgnoreCase) &&
            outcome.Evidence.Any(evidence => evidence.Contains("Should diagnostics include growth trends", StringComparison.OrdinalIgnoreCase)));
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
        Assert.Contains(retiredWithEvidence.Summary.ItemOutcomes, outcome =>
            outcome.Outcome == "RetiredRisk" &&
            outcome.ItemKind == "ActiveRisk" &&
            outcome.Rule == "explicit-risk-retirement" &&
            outcome.Rationale.Contains("explicitly retired", StringComparison.OrdinalIgnoreCase) &&
            outcome.Evidence.Any(evidence => evidence.Contains("Context growth can hide important constraints", StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(retiredWithEvidence.Summary.StableUnderstandingRetentionWarnings, warning =>
            warning.Contains("Active risk disappeared", StringComparison.Ordinal));
        Assert.Contains(retiredWithEvidence.Summary.RevisionSummary, summary =>
            summary.Contains("active risk", StringComparison.OrdinalIgnoreCase));
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

    private static void AssertCompressionOutcome(
        IReadOnlyList<OperationalContextCompressionOutcome> outcomes,
        string outcome,
        string itemKind,
        string rule)
    {
        OperationalContextCompressionOutcome match = outcomes.FirstOrDefault(candidate =>
            candidate.Outcome == outcome &&
            candidate.ItemKind == itemKind &&
            candidate.Rule == rule) ?? throw new Xunit.Sdk.XunitException(
            $"Expected compression outcome '{outcome}' for item kind '{itemKind}' with rule '{rule}'.");
        Assert.False(string.IsNullOrWhiteSpace(match.Threshold));
        Assert.False(string.IsNullOrWhiteSpace(match.Rationale));
        Assert.NotEmpty(match.Evidence);
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
}
