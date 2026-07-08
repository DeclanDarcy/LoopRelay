using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RoadmapTransitionStatusSemanticExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_persists_status_report_transition_slice()
    {
        using var repo = new TempRepo();
        await SeedRepositoryWorkParentAsync(repo);
        await SeedRoadmapStateAsync(repo);
        string beforeState = repo.Read(Cli.RoadmapArtifactPaths.StateJson);

        Cli.RoadmapTransitionStatusSemanticExecutionResult result = await Executor(repo)
            .ExecuteAsync(Cli.RoadmapTransitionStatusSemanticRequest.Default);

        Assert.True(result.Completed);
        Assert.Equal(Cli.RepositoryWorkAdmissionOutcome.ReportOnly, result.AdmissionOutcome);
        Assert.Equal(beforeState, repo.Read(Cli.RoadmapArtifactPaths.StateJson));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.SubjectIdentity));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.ProtocolDefinition));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.Ledger));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.CurrentRoadmapStateSource(result.RunId)));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.StatusObservation(result.RunId)));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.LegacyStatusObservation(result.RunId)));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.Evidence(result.RunId)));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.Decision(result.RunId)));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.Equivalence(result.RunId)));
        Assert.True(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.LatestReport));

        string subject = repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.SubjectIdentity);
        Assert.Contains("RoadmapTransition", subject, StringComparison.Ordinal);
        Assert.Contains("StatusReport", subject, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusAsync", subject, StringComparison.Ordinal);
        Assert.DoesNotContain(repo.Root, subject, StringComparison.OrdinalIgnoreCase);

        string observation = repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.StatusObservation(result.RunId));
        Assert.Contains("Status: EvidenceBlocked.", observation, StringComparison.Ordinal);
        Assert.Contains("ResolveEvidenceBlocker -> EvidenceBlocked", observation, StringComparison.Ordinal);
        Assert.Contains("Observation Authority | none", observation, StringComparison.Ordinal);

        string decision = repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.Decision(result.RunId));
        Assert.Contains("\"AcceptedChoice\": \"emit-report-only\"", decision, StringComparison.Ordinal);
        Assert.Contains("\"AuthorizedEffect\": \"report artifact only\"", decision, StringComparison.Ordinal);
        Assert.Contains("\"StateMutationDetected\": false", decision, StringComparison.Ordinal);

        string equivalence = repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.Equivalence(result.RunId));
        Assert.Contains("\"Accepted\": true", equivalence, StringComparison.Ordinal);
        Assert.Contains("raw-status-output", equivalence, StringComparison.Ordinal);
        Assert.Contains("roadmap-state-non-mutation", equivalence, StringComparison.Ordinal);

        string report = repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.LatestReport);
        Assert.Contains("RoadmapTransition:StatusReport Semantic Report", report, StringComparison.Ordinal);
        Assert.Contains("Report Authority | none", report, StringComparison.Ordinal);
        Assert.Contains("Raw observation", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_keeps_legacy_status_available()
    {
        using var repo = new TempRepo();
        await SeedRepositoryWorkParentAsync(repo);
        await SeedRoadmapStateAsync(repo);

        _ = await Executor(repo).ExecuteAsync(Cli.RoadmapTransitionStatusSemanticRequest.Default);

        var console = new TestConsole();
        Cli.RoadmapOutcome outcome = await StateMachineFactory.Create(
            repo,
            new ScriptedAgentRuntime(),
            console).StatusAsync(CancellationToken.None);

        Assert.Equal(Cli.RoadmapOutcome.Paused, outcome);
        Assert.Contains(console.Infos, line => line.Contains("Status: EvidenceBlocked.", StringComparison.Ordinal));
        Assert.Contains(console.Warnings, line => line.Contains("Evidence missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_denies_status_report_when_report_authority_is_missing()
    {
        using var repo = new TempRepo();
        await SeedRepositoryWorkParentAsync(repo);
        var request = Cli.RoadmapTransitionStatusSemanticRequest.Default with
        {
            AuthorityScopes = Cli.RoadmapTransitionStatusAuthorityScopes.DefaultReportOnlyScopes
                .Where(scope => scope != Cli.RoadmapTransitionStatusAuthorityScopes.Report)
                .ToArray(),
        };

        Cli.RoadmapTransitionStatusSemanticExecutionResult result = await Executor(repo).ExecuteAsync(request);

        Assert.Equal(Cli.RepositoryWorkAdmissionOutcome.Denied, result.AdmissionOutcome);
        Assert.False(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.StatusObservation(result.RunId)));
        Assert.False(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.Evidence(result.RunId)));
        Assert.Contains("missing the required report-only scope", repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.Admission(result.RunId)), StringComparison.Ordinal);
        Assert.Contains("\"Outcome\": \"Denied\"", repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.Ledger), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_mutation_authority_for_report_only_status()
    {
        using var repo = new TempRepo();
        await SeedRepositoryWorkParentAsync(repo);
        var request = Cli.RoadmapTransitionStatusSemanticRequest.Default with
        {
            AuthorityScopes =
            [
                ..Cli.RoadmapTransitionStatusAuthorityScopes.DefaultReportOnlyScopes,
                Cli.RoadmapTransitionStatusAuthorityScopes.RoadmapStateWrite,
            ],
        };

        Cli.RoadmapTransitionStatusSemanticExecutionResult result = await Executor(repo).ExecuteAsync(request);

        Assert.Equal(Cli.RepositoryWorkAdmissionOutcome.Denied, result.AdmissionOutcome);
        string admission = repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.Admission(result.RunId));
        Assert.Contains("Report-only status cannot request mutation", admission, StringComparison.Ordinal);
        Assert.DoesNotContain("status-output", repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.Ledger), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_reports_unsupported_transition_key_without_status_execution()
    {
        using var repo = new TempRepo();
        await SeedRepositoryWorkParentAsync(repo);
        var request = Cli.RoadmapTransitionStatusSemanticRequest.Default with
        {
            TransitionKey = "UnblockReview",
        };

        Cli.RoadmapTransitionStatusSemanticExecutionResult result = await Executor(repo).ExecuteAsync(request);

        Assert.Equal(Cli.RepositoryWorkAdmissionOutcome.Unsupported, result.AdmissionOutcome);
        Assert.False(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.StatusObservation(result.RunId)));
        Assert.Contains("does not support transition key", repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.Admission(result.RunId)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_blocks_malformed_current_state_before_status_execution()
    {
        using var repo = new TempRepo();
        await SeedRepositoryWorkParentAsync(repo);
        repo.Write(Cli.RoadmapArtifactPaths.StateJson, "{ not valid json");

        Cli.RoadmapTransitionStatusSemanticExecutionResult result = await Executor(repo)
            .ExecuteAsync(Cli.RoadmapTransitionStatusSemanticRequest.Default);

        Assert.Equal(Cli.RepositoryWorkAdmissionOutcome.Blocked, result.AdmissionOutcome);
        Assert.False(Exists(repo, Cli.RoadmapTransitionStatusSemanticArtifactPaths.StatusObservation(result.RunId)));
        Assert.Contains("\"SourceCondition\": \"malformed\"", repo.Read(Cli.RoadmapTransitionStatusSemanticArtifactPaths.CurrentRoadmapStateSource(result.RunId)), StringComparison.Ordinal);
    }

    private static Cli.RoadmapTransitionStatusSemanticExecutor Executor(TempRepo repo) =>
        new(
            repo.Artifacts,
            new Cli.RoadmapStateStore(repo.Artifacts),
            new Cli.RoadmapStartupPlanner(),
            new TestConsole());

    private static async Task SeedRepositoryWorkParentAsync(TempRepo repo)
    {
        repo.Write("plan.md", "# Plan\n\nSeed RepositoryWork parent subject.");
        _ = await new Cli.RepositoryWorkSemanticExecutor(repo.Artifacts, new TestConsole())
            .ExecuteAsync(Cli.RepositoryWorkSemanticRequest.Default);
    }

    private static async Task SeedRoadmapStateAsync(TempRepo repo)
    {
        var state = new Cli.RoadmapStateDocument(
            Cli.RoadmapState.EvidenceBlocked,
            [],
            new Cli.RoadmapTransitionSummary(
                Cli.RoadmapState.CoreReady,
                Cli.RoadmapState.EvidenceBlocked,
                "EvidenceReview",
                "None",
                "evidence.md",
                "Blocked",
                Cli.TransitionStatus.Paused,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
            [new Cli.BlockerRow("Evidence missing", "Gather evidence and rerun.")],
            "decision-0001",
            0,
            0,
            new Cli.ProjectionManifestCounts(0, 0, 0),
            new Cli.RoadmapTransitionIntent(
                "ResolveEvidenceBlocker",
                Cli.RoadmapState.EvidenceBlocked,
                ["evidence.md"]),
            ["Resolve blocker and rerun"],
            []);
        await new Cli.RoadmapStateStore(repo.Artifacts).SaveAsync(state);
    }

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
