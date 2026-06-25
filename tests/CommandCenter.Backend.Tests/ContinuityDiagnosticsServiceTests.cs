using CommandCenter.Core.Artifacts;
using CommandCenter.Core.Configuration;
using CommandCenter.Continuity;
using CommandCenter.Continuity.Models;
using CommandCenter.Continuity.Primitives;
using CommandCenter.Continuity.Services;
using CommandCenter.Core.Repositories;

namespace CommandCenter.Backend.Tests;

public sealed class ContinuityDiagnosticsServiceTests
{
    [Fact]
    public async Task RevisionTrackingReadsCurrentAndHistoricalOperationalContexts()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.0004.md", """
            # Operational Context

            ## Architecture

            - Backend owns workflow authority.
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Architecture

            - Backend owns workflow authority.

            ## Constraints

            - Metrics remain observational.
            """);

        ContinuityDiagnostics diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(2, diagnostics.RevisionCount);
        Assert.Equal(5, diagnostics.EvolutionLedger.CurrentRevision?.RevisionNumber);
        Assert.True(diagnostics.CurrentContextByteCount > 0);
        Assert.True(diagnostics.CurrentContextCharacterCount > 0);
        Assert.Equal(1, diagnostics.EvolutionLedger.CurrentRevision?.ArchitectureItemCount);
        Assert.Equal(1, diagnostics.EvolutionLedger.CurrentRevision?.ConstraintCount);
    }

    [Fact]
    public async Task ConstraintDecisionAndRationaleLossAreDetected()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.0001.md", """
            # Operational Context

            ## Constraints

            - Human review gates promotion.

            ## Stable Decisions

            - Decision: Repository artifacts are authoritative.

            ## Decision Rationale

            - Repository artifacts survive restarts.
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Stable Decisions
            """);

        ContinuityDiagnostics diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(1, diagnostics.ConstraintTrend.LostCount);
        Assert.Equal(1, diagnostics.DecisionTrend.LostCount);
        Assert.Equal(1, diagnostics.RationaleTrend.LostCount);
    }

    [Fact]
    public async Task IdentityAwareModificationsAreProjectedThroughEvolutionDiagnostics()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.0001.md", """
            # Operational Context

            ## Constraints

            - Backend workflow projection must own operational lifecycle status.
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Backend workflow projection must own operational lifecycle status, gate evidence, and recovery hints.
            """);

        ContinuityDiagnostics diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(1, diagnostics.OperationalEvolution.ModifiedCount);
        Assert.Equal(1, diagnostics.ConstraintTrend.ModifiedCount);
        Assert.Equal(0, diagnostics.ConstraintTrend.AddedCount);
        Assert.Equal(0, diagnostics.ConstraintTrend.RemovedCount);
        OperationalContextSemanticChange change = Assert.Single(
            diagnostics.OperationalEvolution.SemanticChanges,
            change => change.Type == OperationalContextSemanticChangeType.ModifiedConstraint);
        Assert.Equal("section-semantic-lineage", change.IdentityBasis);
        Assert.Equal("Backend workflow projection must own operational lifecycle status.", change.PreviousState);
        Assert.Equal("Backend workflow projection must own operational lifecycle status, gate evidence, and recovery hints.", change.CurrentState);
        Assert.Contains(change.SupportingEvidence, evidence =>
            evidence.Contains("Semantic lineage key", StringComparison.OrdinalIgnoreCase));
        ContinuityDiagnosticGroup modificationGroup = Assert.Single(
            diagnostics.DiagnosticGroups,
            group => group.Title == "Modified operational-context item");
        Assert.Contains("Identity basis: section-semantic-lineage.", modificationGroup.Diagnostics);
        Assert.Contains(modificationGroup.Diagnostics, diagnostic =>
            diagnostic.Contains("Previous state: Backend workflow projection must own operational lifecycle status.", StringComparison.Ordinal));
        Assert.Contains(modificationGroup.Diagnostics, diagnostic =>
            diagnostic.Contains("Current state: Backend workflow projection must own operational lifecycle status, gate evidence, and recovery hints.", StringComparison.Ordinal));
        Assert.Contains(modificationGroup.Diagnostics, diagnostic =>
            diagnostic.Contains("Supporting evidence: Semantic lineage key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task OpenQuestionResolutionIsDistinguishedFromDisappearance()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.0001.md", """
            # Operational Context

            ## Open Questions

            - Should diagnostics include growth trends?
            - Should missing rationale block promotion?
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Recent Understanding Changes

            - Resolved question: Should diagnostics include growth trends?
            """);

        ContinuityDiagnostics diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(2, diagnostics.OpenQuestionTrend.RemovedCount);
        Assert.Equal(1, diagnostics.OpenQuestionTrend.ResolvedCount);
        Assert.Equal(1, diagnostics.OpenQuestionTrend.LostCount);
    }

    [Fact]
    public async Task ActiveRiskResolutionIsDistinguishedFromDisappearance()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.0001.md", """
            # Operational Context

            ## Active Risks

            - Metrics could become workflow gates.
            - Rationale could disappear silently.
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Recent Understanding Changes

            - Retired risk: Metrics could become workflow gates.
            """);

        ContinuityDiagnostics diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(2, diagnostics.ActiveRiskTrend.RemovedCount);
        Assert.Equal(1, diagnostics.ActiveRiskTrend.ResolvedCount);
        Assert.Equal(1, diagnostics.ActiveRiskTrend.LostCount);
    }

    [Fact]
    public async Task CompressionMetricsAreCalculatedFromProposalSummaries()
    {
        Harness harness = await CreateHarnessAsync();
        await harness.ProposalStore.SaveAsync(harness.Repository, new OperationalContextProposal
        {
            ProposalId = "proposal-1",
            RepositoryId = harness.Repository.Id,
            GeneratedAt = DateTimeOffset.UtcNow,
            BaselineCurrentContextHash = "baseline",
            GeneratedContentHash = "generated",
            CompressionSummary = new OperationalContextCompressionSummary
            {
                CompressedItemCount = 3,
                RemovedItemCount = 2,
                ResolvedQuestionCount = 1,
                RetiredRiskCount = 1,
                Warnings = ["Rationale disappeared."],
                NoiseRemovedIndicators = ["Repeated investigation detail removed."]
            }
        }, "# Operational Context");

        ContinuityDiagnostics diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(1, diagnostics.CompressionTrend.ProposalCount);
        Assert.Equal(3, diagnostics.CompressionTrend.CompressedItemCount);
        Assert.Equal(2, diagnostics.CompressionTrend.RemovedItemCount);
        Assert.Equal(1, diagnostics.CompressionTrend.ResolvedQuestionCount);
        Assert.Equal(1, diagnostics.CompressionTrend.RetiredRiskCount);
        Assert.Contains("Rationale disappeared.", diagnostics.CompressionTrend.Warnings);
        Assert.Contains("Repeated investigation detail removed.", diagnostics.CompressionTrend.NoiseRemovedIndicators);
    }

    [Fact]
    public async Task DiagnosticGroupsAreNormalizedByContinuityCategory()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.0001.md", """
            # Operational Context

            ## Constraints

            - Backend diagnostics may infer continuity status in React.

            ## Open Questions

            - Should continuity diagnostics be grouped?
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Backend diagnostics must project grouped continuity status for React.

            ## Recent Understanding Changes

            - Resolved question: Should continuity diagnostics be grouped?
            """);
        await harness.ProposalStore.SaveAsync(harness.Repository, new OperationalContextProposal
        {
            ProposalId = "proposal-1",
            RepositoryId = harness.Repository.Id,
            GeneratedAt = DateTimeOffset.UtcNow,
            Status = OperationalContextProposalStatus.Rejected,
            BaselineCurrentContextHash = "baseline",
            GeneratedContentHash = "generated",
            DecisionAssimilation = new DecisionAssimilationProjection
            {
                Decisions =
                [
                    new DecisionAssimilationRecord
                    {
                        DecisionId = "decision-1",
                        Statement = "Backend owns continuity grouping.",
                        Taxonomy = DecisionTaxonomy.ArchitecturalDecision,
                        TaxonomyBasis = new DecisionTaxonomyBasis
                        {
                            Taxonomy = DecisionTaxonomy.ArchitecturalDecision,
                            MatchedRules = ["architecture-rule"],
                            MatchedEvidence = ["Backend owns continuity grouping."],
                            Diagnostics = ["Matched architecture ownership."]
                        },
                        Status = DecisionAssimilationStatus.Assimilated,
                        IsDurable = true,
                        QualifiesForAssimilation = true,
                        IsAssimilated = true,
                        OperationalStatement = "Continuity grouping remains backend-owned.",
                        SourceEvidence = ["Decision evidence."]
                    }
                ],
                Limit = new DecisionAssimilationLimit
                {
                    Limit = 1,
                    Reason = "Keep context compact.",
                    TotalAnalyzedItemCount = 1,
                    TotalQualifyingItemCount = 1,
                    AssimilatedItemCount = 1
                },
                Contradictions =
                [
                    new ContinuityDecisionContradiction
                    {
                        ContradictionId = "contradiction-1",
                        DecisionA = new ContinuityDecisionReference
                        {
                            DecisionId = "decision-a",
                            Statement = "React owns continuity grouping.",
                            Taxonomy = DecisionTaxonomy.TacticalDecision
                        },
                        DecisionB = new ContinuityDecisionReference
                        {
                            DecisionId = "decision-b",
                            Statement = "Backend owns continuity grouping.",
                            Taxonomy = DecisionTaxonomy.ArchitecturalDecision
                        },
                        ConflictType = DecisionContradictionConflictType.DirectNegation,
                        ConflictEvidence = ["Ownership conflict."],
                        Severity = DecisionContradictionSeverity.High,
                        ResolutionGuidance = "Use backend projection."
                    }
                ]
            },
            CompressionSummary = new OperationalContextCompressionSummary
            {
                CompressedItemCount = 1,
                RemovedItemCount = 1,
                NoiseRemovedIndicators = ["Repeated diagnostic wording removed."]
            },
            Review = new OperationalContextReview
            {
                ProposalId = "proposal-1",
                StaleReason = "Baseline changed before promotion."
            }
        }, "# Operational Context");

        ContinuityDiagnostics diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        string[] categories = diagnostics.DiagnosticGroups.Select(group => group.Category).ToArray();
        Assert.Contains("assimilation", categories);
        Assert.Contains("compression", categories);
        Assert.Contains("evolution", categories);
        Assert.Contains("diff", categories);
        Assert.Contains("recovery", categories);
        Assert.Contains("classification", categories);
        Assert.Contains("contradictions", categories);
        Assert.Contains("lost understanding", categories);
        Assert.Contains("resolved understanding", categories);
        Assert.Contains(diagnostics.DiagnosticGroups, group =>
            group.Category == "assimilation" &&
            group.Diagnostics.Any(diagnostic => diagnostic.Contains("decision-1", StringComparison.Ordinal)));
        Assert.Contains(diagnostics.DiagnosticGroups, group =>
            group.Category == "classification" &&
            group.Diagnostics.Any(diagnostic => diagnostic.Contains("rules=1", StringComparison.Ordinal)));
        Assert.Contains(diagnostics.DiagnosticGroups, group =>
            group.Category == "contradictions" &&
            group.Diagnostics.Any(diagnostic => diagnostic.Contains("contradiction-1", StringComparison.Ordinal)));
        Assert.Contains(diagnostics.DiagnosticGroups, group =>
            group.Category == "recovery" &&
            group.Diagnostics.Any(diagnostic => diagnostic.Contains("Baseline changed before promotion.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task RepeatedInvestigationQuestionAndDecisionReworkIndicatorsCanBeObserved()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.0001.md", """
            # Operational Context

            ## Open Questions

            - Should reports include trend deltas?

            ## Recent Understanding Changes

            - Investigation repeated for context drift.
            - Decision rework: diagnostics wording changed.
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Open Questions

            - Should reports include trend deltas?

            ## Recent Understanding Changes

            - Investigation repeated for context drift.
            - Decision rework: diagnostics wording changed.
            """);

        ContinuityDiagnostics diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Contains("Investigation repeated for context drift.", diagnostics.RepeatedInvestigationIndicators);
        Assert.Contains("Should reports include trend deltas?", diagnostics.RepeatedQuestionIndicators);
        Assert.Contains("Decision rework: diagnostics wording changed.", diagnostics.DecisionReworkIndicators);
    }

    [Fact]
    public async Task ReportGenerationWritesDiagnosticArtifactWithoutMutatingCurrentContext()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Reports are read-only diagnostics.
            """);
        string before = await ReadAsync(harness.Repository, ".agents/operational_context.md");

        ContinuityReport report = await harness.ReportService.GenerateReportAsync(harness.Repository.Id);
        IReadOnlyList<ContinuityReport> reports = await harness.ReportService.ListReportsAsync(harness.Repository.Id);
        string after = await ReadAsync(harness.Repository, ".agents/operational_context.md");

        Assert.Equal(before, after);
        Assert.StartsWith(".agents/operational_context/reports/continuity.", report.RelativePath, StringComparison.Ordinal);
        Assert.EndsWith(".json", report.RelativePath, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(harness.Repository.Path, report.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Contains(reports, listed => listed.ReportId == report.ReportId);
    }

    [Fact]
    public async Task ReportGenerationPersistsStructuredModificationDiagnostics()
    {
        Harness harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.0001.md", """
            # Operational Context

            ## Constraints

            - Backend workflow projection must own operational lifecycle status.
            """);
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Backend workflow projection must own operational lifecycle status and gate evidence.
            """);

        ContinuityReport report = await harness.ReportService.GenerateReportAsync(harness.Repository.Id);
        IReadOnlyList<ContinuityReport> reports = await harness.ReportService.ListReportsAsync(harness.Repository.Id);

        ContinuityReport listed = Assert.Single(reports);
        Assert.Equal(report.ReportId, listed.ReportId);
        Assert.Equal(1, listed.Diagnostics.OperationalEvolution.ModifiedCount);
        Assert.Contains(listed.Diagnostics.DiagnosticGroups, group =>
            group.Title == "Operational evolution" &&
            group.Diagnostics.Contains("Modified item count: 1."));
        Assert.Contains(listed.Diagnostics.DiagnosticGroups, group =>
            group.Title == "Modified operational-context item" &&
            group.Diagnostics.Any(diagnostic => diagnostic.Contains("Identity basis: section-semantic-lineage.", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ReportListingSkipsCorruptReportArtifacts()
    {
        Harness harness = await CreateHarnessAsync();
        ContinuityReport report = await harness.ReportService.GenerateReportAsync(harness.Repository.Id);
        await WriteAsync(
            harness.Repository,
            ".agents/operational_context/reports/continuity.19990101000000000.json",
            "{ not valid json");

        IReadOnlyList<ContinuityReport> reports = await harness.ReportService.ListReportsAsync(harness.Repository.Id);

        Assert.Single(reports);
        Assert.Equal(report.ReportId, reports[0].ReportId);
    }

    private static async Task<Harness> CreateHarnessAsync()
    {
        string repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        Repository repository = await repositoryService.RegisterAsync(repositoryPath);
        var artifactStore = new FileSystemArtifactStore();
        var artifactService = new ArtifactService(artifactStore);
        var proposalStore = new FileSystemOperationalContextProposalStore(artifactStore);
        var parser = new MarkdownOperationalContextParser();
        var diagnosticsService = new ContinuityDiagnosticsService(
            repositoryService,
            artifactService,
            artifactStore,
            parser,
            new UnderstandingDiffService(),
            proposalStore);
        var reportService = new ContinuityReportService(
            repositoryService,
            artifactStore,
            diagnosticsService);

        return new Harness(
            repository,
            proposalStore,
            diagnosticsService,
            reportService);
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
        FileSystemOperationalContextProposalStore ProposalStore,
        ContinuityDiagnosticsService DiagnosticsService,
        ContinuityReportService ReportService);
}
