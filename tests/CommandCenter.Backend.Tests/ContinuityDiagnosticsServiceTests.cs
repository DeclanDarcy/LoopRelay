using CommandCenter.Backend.Artifacts;
using CommandCenter.Backend.Configuration;
using CommandCenter.Backend.Continuity;
using CommandCenter.Backend.Repositories;

namespace CommandCenter.Backend.Tests;

public sealed class ContinuityDiagnosticsServiceTests
{
    [Fact]
    public async Task RevisionTrackingReadsCurrentAndHistoricalOperationalContexts()
    {
        var harness = await CreateHarnessAsync();
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

        var diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

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
        var harness = await CreateHarnessAsync();
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

        var diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(1, diagnostics.ConstraintTrend.LostCount);
        Assert.Equal(1, diagnostics.DecisionTrend.LostCount);
        Assert.Equal(1, diagnostics.RationaleTrend.LostCount);
    }

    [Fact]
    public async Task OpenQuestionResolutionIsDistinguishedFromDisappearance()
    {
        var harness = await CreateHarnessAsync();
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

        var diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(2, diagnostics.OpenQuestionTrend.RemovedCount);
        Assert.Equal(1, diagnostics.OpenQuestionTrend.ResolvedCount);
        Assert.Equal(1, diagnostics.OpenQuestionTrend.LostCount);
    }

    [Fact]
    public async Task ActiveRiskResolutionIsDistinguishedFromDisappearance()
    {
        var harness = await CreateHarnessAsync();
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

        var diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(2, diagnostics.ActiveRiskTrend.RemovedCount);
        Assert.Equal(1, diagnostics.ActiveRiskTrend.ResolvedCount);
        Assert.Equal(1, diagnostics.ActiveRiskTrend.LostCount);
    }

    [Fact]
    public async Task CompressionMetricsAreCalculatedFromProposalSummaries()
    {
        var harness = await CreateHarnessAsync();
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

        var diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Equal(1, diagnostics.CompressionTrend.ProposalCount);
        Assert.Equal(3, diagnostics.CompressionTrend.CompressedItemCount);
        Assert.Equal(2, diagnostics.CompressionTrend.RemovedItemCount);
        Assert.Equal(1, diagnostics.CompressionTrend.ResolvedQuestionCount);
        Assert.Equal(1, diagnostics.CompressionTrend.RetiredRiskCount);
        Assert.Contains("Rationale disappeared.", diagnostics.CompressionTrend.Warnings);
        Assert.Contains("Repeated investigation detail removed.", diagnostics.CompressionTrend.NoiseRemovedIndicators);
    }

    [Fact]
    public async Task RepeatedInvestigationQuestionAndDecisionReworkIndicatorsCanBeObserved()
    {
        var harness = await CreateHarnessAsync();
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

        var diagnostics = await harness.DiagnosticsService.GetDiagnosticsAsync(harness.Repository.Id);

        Assert.Contains("Investigation repeated for context drift.", diagnostics.RepeatedInvestigationIndicators);
        Assert.Contains("Should reports include trend deltas?", diagnostics.RepeatedQuestionIndicators);
        Assert.Contains("Decision rework: diagnostics wording changed.", diagnostics.DecisionReworkIndicators);
    }

    [Fact]
    public async Task ReportGenerationWritesDiagnosticArtifactWithoutMutatingCurrentContext()
    {
        var harness = await CreateHarnessAsync();
        await WriteAsync(harness.Repository, ".agents/operational_context.md", """
            # Operational Context

            ## Constraints

            - Reports are read-only diagnostics.
            """);
        var before = await ReadAsync(harness.Repository, ".agents/operational_context.md");

        var report = await harness.ReportService.GenerateReportAsync(harness.Repository.Id);
        var reports = await harness.ReportService.ListReportsAsync(harness.Repository.Id);
        var after = await ReadAsync(harness.Repository, ".agents/operational_context.md");

        Assert.Equal(before, after);
        Assert.StartsWith(".agents/operational_context/reports/continuity.", report.RelativePath, StringComparison.Ordinal);
        Assert.EndsWith(".json", report.RelativePath, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(harness.Repository.Path, report.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Contains(reports, listed => listed.ReportId == report.ReportId);
    }

    [Fact]
    public async Task ReportListingSkipsCorruptReportArtifacts()
    {
        var harness = await CreateHarnessAsync();
        var report = await harness.ReportService.GenerateReportAsync(harness.Repository.Id);
        await WriteAsync(
            harness.Repository,
            ".agents/operational_context/reports/continuity.19990101000000000.json",
            "{ not valid json");

        var reports = await harness.ReportService.ListReportsAsync(harness.Repository.Id);

        Assert.Single(reports);
        Assert.Equal(report.ReportId, reports[0].ReportId);
    }

    private static async Task<Harness> CreateHarnessAsync()
    {
        var repositoryPath = CreateGitRepositoryDirectory();
        var repositoryService = new RepositoryService(
            new ApplicationConfigurationStore(Path.Combine(CreateTemporaryDirectory(), "configuration.json")));
        var repository = await repositoryService.RegisterAsync(repositoryPath);
        var artifactStore = new FileSystemArtifactStore();
        var artifactService = new ArtifactService(artifactStore);
        var proposalStore = new FileSystemOperationalContextProposalStore(artifactStore);
        var parser = new MarkdownOperationalContextParser();
        var diagnosticsService = new ContinuityDiagnosticsService(
            repositoryService,
            artifactService,
            artifactStore,
            parser,
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
        FileSystemOperationalContextProposalStore ProposalStore,
        ContinuityDiagnosticsService DiagnosticsService,
        ContinuityReportService ReportService);
}
