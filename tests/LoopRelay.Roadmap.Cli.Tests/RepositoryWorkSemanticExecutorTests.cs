using LoopRelay.Core.Artifacts;
using LoopRelay.Core.Repositories;
using LoopRelay.Roadmap.Cli;

namespace LoopRelay.Roadmap.Cli.Tests;

public sealed class RepositoryWorkSemanticExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_persists_repository_work_vertical_slice()
    {
        using var repo = new TempRepo();
        repo.Write("plan.md", "# Plan\n\nExecute the semantic architecture slice.");

        Cli.RepositoryWorkSemanticExecutionResult result = await new Cli.RepositoryWorkSemanticExecutor(
            repo.Artifacts,
            new TestConsole()).ExecuteAsync(Cli.RepositoryWorkSemanticRequest.Default);

        Assert.True(result.Completed);
        Assert.Equal(Cli.RepositoryWorkAdmissionOutcome.Admitted, result.AdmissionOutcome);
        Assert.True(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.SubjectIdentity));
        Assert.True(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.Ledger));
        Assert.True(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.CurrentSummary));
        Assert.True(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.CurrentState));
        Assert.True(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.CurrentUnderstanding));
        Assert.True(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.CapabilityConformanceReport));
        Assert.True(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.CompletionCertification));

        string subject = repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.SubjectIdentity);
        Assert.DoesNotContain(repo.Root, subject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(repo.Repository.Id.ToString(), subject, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-subjectless-interaction", subject, StringComparison.Ordinal);
        Assert.Contains("report-fields-do-not-create-authority", subject, StringComparison.Ordinal);

        string ledger = repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.Ledger);
        Assert.Contains("RepositoryWork semantic path executed", ledger, StringComparison.Ordinal);
        Assert.Contains("candidate-artifact-promotion", ledger, StringComparison.Ordinal);
        Assert.Contains("current-semantic-summary-missing-or-stale", ledger, StringComparison.Ordinal);
        Assert.Contains("\"LifecycleMovement\": \"CompletionCertified\"", ledger, StringComparison.Ordinal);
        Assert.Contains("\"Outcome\": \"Accepted\"", ledger, StringComparison.Ordinal);

        string summary = repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.CurrentSummary);
        Assert.Contains("Report Authority | none", summary, StringComparison.Ordinal);
        Assert.Contains("Candidate artifacts cannot become current by being written", summary, StringComparison.Ordinal);

        string state = repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.CurrentState);
        Assert.Contains("\"State\": \"StateCurrent\"", state, StringComparison.Ordinal);

        string conformance = repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.CapabilityConformanceReport);
        Assert.Contains("| Outcome | Accepted |", conformance, StringComparison.Ordinal);
        Assert.Contains("does not grant runtime authority", conformance, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_denies_execution_when_required_authority_scope_is_missing()
    {
        using var repo = new TempRepo();
        repo.Write("plan.md", "# Plan\n\nExecute the semantic architecture slice.");
        var request = Cli.RepositoryWorkSemanticRequest.Default with
        {
            AuthorityScopes = Cli.RepositoryWorkAuthorityScopes.DefaultExecutionScopes
                .Where(scope => scope != Cli.RepositoryWorkAuthorityScopes.ArtifactPromotion)
                .ToArray(),
        };

        Cli.RepositoryWorkSemanticExecutionResult result = await new Cli.RepositoryWorkSemanticExecutor(repo.Artifacts)
            .ExecuteAsync(request);

        Assert.False(result.Completed);
        Assert.Equal(Cli.RepositoryWorkAdmissionOutcome.Denied, result.AdmissionOutcome);
        Assert.False(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.CurrentSummary));

        string admission = repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.Admission(result.RunId));
        Assert.Contains("repositorywork.artifact-promotion", admission, StringComparison.Ordinal);
        Assert.Contains("missing the required scope", admission, StringComparison.Ordinal);

        string ledger = repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.Ledger);
        Assert.Contains("\"Promotion\": null", ledger, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_reports_unsupported_operation_without_running_interaction()
    {
        using var repo = new TempRepo();
        repo.Write("plan.md", "# Plan\n\nExecute the semantic architecture slice.");
        var request = new Cli.RepositoryWorkSemanticRequest(
            "RepositoryWorkUnknownOperation",
            "Try an unsupported governed operation.",
            "plan.md",
            Cli.RepositoryWorkAuthorityScopes.DefaultExecutionScopes);

        Cli.RepositoryWorkSemanticExecutionResult result = await new Cli.RepositoryWorkSemanticExecutor(repo.Artifacts)
            .ExecuteAsync(request);

        Assert.Equal(Cli.RepositoryWorkAdmissionOutcome.Unsupported, result.AdmissionOutcome);
        Assert.False(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.CurrentSummary));
        Assert.Contains("Unsupported", repo.Read(result.ReportPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_report_operation_is_report_only_and_does_not_promote_artifacts()
    {
        using var repo = new TempRepo();
        var request = new Cli.RepositoryWorkSemanticRequest(
            Cli.RepositoryWorkSemanticRequest.ReportOperation,
            "Inspect RepositoryWork subject identity.",
            "plan.md",
            [Cli.RepositoryWorkAuthorityScopes.RepositoryRead, Cli.RepositoryWorkAuthorityScopes.Report]);

        Cli.RepositoryWorkSemanticExecutionResult result = await new Cli.RepositoryWorkSemanticExecutor(repo.Artifacts)
            .ExecuteAsync(request);

        Assert.Equal(Cli.RepositoryWorkAdmissionOutcome.ReportOnly, result.AdmissionOutcome);
        Assert.False(Exists(repo, Cli.RepositoryWorkSemanticArtifactPaths.CurrentSummary));
        Assert.Contains("ReportOnly", repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.Ledger), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_reuses_existing_subject_identity_across_transient_repository_ids()
    {
        using var repo = new TempRepo();
        repo.Write("plan.md", "# Plan\n\nExecute the semantic architecture slice.");

        Cli.RepositoryWorkSemanticExecutionResult first = await new Cli.RepositoryWorkSemanticExecutor(repo.Artifacts)
            .ExecuteAsync(Cli.RepositoryWorkSemanticRequest.Default);
        string firstSubject = repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.SubjectIdentity);

        var sameWorkspaceDifferentProcessIdentity = new Repository
        {
            Id = Guid.NewGuid(),
            Name = repo.Repository.Name,
            Path = repo.Root,
        };
        var artifacts = new Cli.RoadmapArtifacts(new FileSystemArtifactStore(), sameWorkspaceDifferentProcessIdentity);
        Cli.RepositoryWorkSemanticExecutionResult second = await new Cli.RepositoryWorkSemanticExecutor(artifacts)
            .ExecuteAsync(Cli.RepositoryWorkSemanticRequest.Default);

        Assert.Equal(first.SubjectId, second.SubjectId);
        Assert.Equal(firstSubject, repo.Read(Cli.RepositoryWorkSemanticArtifactPaths.SubjectIdentity));
    }

    private static bool Exists(TempRepo repo, string relativePath) =>
        File.Exists(Path.Combine(repo.Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
