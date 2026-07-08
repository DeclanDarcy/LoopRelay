using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Models.NonImplementationLedger;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationCompletion;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Services.RepositorySlices;

namespace LoopRelay.Orchestration.Tests.Services.NonImplementationReview;

public sealed class NonImplementationPostExecutionReviewTests
{
    private static readonly NonImplementationSemanticConfirmerOptions TestOptions =
        new("ConfirmNonImplementationCandidate", "prompt-hash", 32768);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static NonImplementationPostExecutionReviewTests()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public async Task Review_writes_evidence_and_ledgers_generated_root_markdown()
    {
        var artifacts = new InMemoryArtifactStore();
        var runner = new RecordingReviewRunner(request =>
            ResponseFromRequest(request, NonImplementationSemanticDisposition.ConfirmedNonImplementation));
        NonImplementationPostExecutionReviewService service = NewService(artifacts, runner, out var ledger);
        RepositorySliceBaseline baseline = Baseline();
        RepositorySliceSnapshot postSnapshot = Snapshot(File("ROOT_NOTE.md", "??", "hash-root"));

        NonImplementationPostExecutionReviewResult result =
            await service.ReviewAsync(baseline, postSnapshot, CancellationToken.None);

        Assert.Equal("slice-test", result.ExecutionSliceId);
        Assert.Equal(1, result.Summary.ChangedFileCount);
        Assert.Equal(1, result.Summary.SemanticCandidateCount);
        Assert.Equal(1, result.Summary.ConfirmedCount);
        Assert.Contains(OrchestrationArtifactPaths.NonImplementationSliceReview("slice-test"), result.EvidencePaths);
        NonImplementationReviewLedgerEntry entry = Assert.Single((await ledger.LoadOrCreateAsync()).Entries);
        Assert.Equal("ROOT_NOTE.md", entry.Path);
        Assert.Equal(NonImplementationSemanticDisposition.ConfirmedNonImplementation, entry.SemanticDisposition);
        Assert.Single(runner.Requests);
        string? evidence = await artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationSliceReview("slice-test"));
        Assert.Contains("ROOT_NOTE.md", evidence);
        Assert.Contains("ConfirmedNonImplementation", evidence);
    }

    [Fact]
    public async Task Review_does_not_ledger_preexisting_dirty_markdown_unchanged_by_execution()
    {
        var artifacts = new InMemoryArtifactStore();
        var runner = new RecordingReviewRunner(_ => throw new InvalidOperationException("Should not review."));
        NonImplementationPostExecutionReviewService service = NewService(artifacts, runner, out var ledger);
        RepositoryFileSnapshotEntry dirtyMarkdown = File("docs/design.md", " M", "hash-existing");
        RepositorySliceBaseline baseline = Baseline(dirtyMarkdown);
        RepositorySliceSnapshot postSnapshot = Snapshot(dirtyMarkdown);

        NonImplementationPostExecutionReviewResult result =
            await service.ReviewAsync(baseline, postSnapshot, CancellationToken.None);

        Assert.Equal(0, result.Summary.ChangedFileCount);
        Assert.Equal(0, result.Summary.SemanticCandidateCount);
        Assert.Empty((await ledger.LoadOrCreateAsync()).Entries);
        Assert.Empty(runner.Requests);
    }

    [Fact]
    public async Task Review_failure_writes_failure_evidence()
    {
        var artifacts = new InMemoryArtifactStore();
        var runner = new RecordingReviewRunner(_ => throw new InvalidOperationException("semantic runner offline"));
        NonImplementationPostExecutionReviewService service = NewService(artifacts, runner, out _);
        RepositorySliceBaseline baseline = Baseline();
        RepositorySliceSnapshot postSnapshot = Snapshot(File("ROOT_NOTE.md", "??", "hash-root"));

        NonImplementationPostExecutionReviewException ex =
            await Assert.ThrowsAsync<NonImplementationPostExecutionReviewException>(
                () => service.ReviewAsync(baseline, postSnapshot, CancellationToken.None));

        string failurePath = Assert.Single(ex.EvidencePaths);
        Assert.Equal(OrchestrationArtifactPaths.NonImplementationSliceFailure("slice-test"), failurePath);
        string? failure = await artifacts.ReadAsync(failurePath);
        Assert.Contains("semantic runner offline", failure);
        Assert.Contains("review post-execution non-implementation file changes", failure);
    }

    private static NonImplementationPostExecutionReviewService NewService(
        IArtifactStore artifacts,
        INonImplementationReviewRunner runner,
        out NonImplementationReviewLedgerStore ledger)
    {
        ledger = new NonImplementationReviewLedgerStore(artifacts);
        var baselineStore = new RepositorySliceBaselineStore(
            new RepositoryChangeSetDetector(
                new NeverProcessRunner(),
                new Repository { Id = Guid.NewGuid(), Name = "repo", Path = "C:/repo" }),
            artifacts);
        var confirmer = new NonImplementationSemanticConfirmer(ledger, runner, TestOptions);
        return new NonImplementationPostExecutionReviewService(
            baselineStore,
            new NonImplementationArtifactClassifier(),
            confirmer,
            artifacts);
    }

    private static RepositorySliceBaseline Baseline(params RepositoryFileSnapshotEntry[] files) =>
        new(
            "slice-test",
            new RepositorySliceSnapshot("slice-test", DateTimeOffset.UnixEpoch, files),
            PersistedPath: null);

    private static RepositorySliceSnapshot Snapshot(params RepositoryFileSnapshotEntry[] files) =>
        new("slice-test", DateTimeOffset.UnixEpoch.AddSeconds(1), files);

    private static RepositoryFileSnapshotEntry File(string path, string status, string hash) =>
        new(
            path,
            PreviousPath: null,
            status,
            Exists: true,
            IsDeleted: false,
            Extension: Path.GetExtension(path).ToLowerInvariant(),
            Size: 1,
            ContentSha256: hash,
            TrackedDiffMetadata: Array.Empty<RepositoryGitDiffNameStatus>());

    private static NonImplementationReviewRunnerResponse ResponseFromRequest(
        NonImplementationReviewRunnerRequest request,
        NonImplementationSemanticDisposition disposition)
    {
        using JsonDocument document = JsonDocument.Parse(ExtractInputPayload(request.PromptPayload));
        JsonElement root = document.RootElement;
        string json = JsonSerializer.Serialize(new
        {
            ledgerEntryId = root.GetProperty("ledgerEntryId").GetString(),
            candidatePath = root.GetProperty("candidatePath").GetString(),
            reviewedContentSha256 = root.GetProperty("reviewedContentSha256").GetString(),
            reviewedFileDeleted = false,
            deletedReviewedIdentity = (string?)null,
            disposition,
            rationale = "semantic rationale",
            evidenceExcerptsOrPathFacts = new[] { "path fact" },
            uncertaintyNote = (string?)null,
        }, JsonOptions);
        return new NonImplementationReviewRunnerResponse(json);
    }

    private static string ExtractInputPayload(string prompt)
    {
        const string marker = "```json";
        int start = prompt.IndexOf(marker, StringComparison.Ordinal);
        int contentStart = prompt.IndexOf('\n', start);
        int end = prompt.IndexOf("```", contentStart + 1, StringComparison.Ordinal);
        return prompt[(contentStart + 1)..end].Trim();
    }

    private sealed class RecordingReviewRunner(
        Func<NonImplementationReviewRunnerRequest, NonImplementationReviewRunnerResponse> handler)
        : INonImplementationReviewRunner
    {
        public List<NonImplementationReviewRunnerRequest> Requests { get; } = [];

        public Task<NonImplementationReviewRunnerResponse> RunAsync(
            NonImplementationReviewRunnerRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(handler(request));
        }
    }

    private sealed class NeverProcessRunner : IProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory) =>
            throw new NotSupportedException("ReviewAsync should use supplied snapshots.");

        public Task<IAgentProcess> StartInteractiveAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class InMemoryArtifactStore : IArtifactStore
    {
        private readonly Dictionary<string, string> files = new(StringComparer.Ordinal);

        public Task<bool> ExistsAsync(string path) => Task.FromResult(files.ContainsKey(path));

        public Task<string?> ReadAsync(string path)
        {
            files.TryGetValue(path, out string? content);
            return Task.FromResult(content);
        }

        public Task WriteAsync(string path, string content)
        {
            files[path] = content;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string path)
        {
            files.Remove(path);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync(string path, string searchPattern) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        public Task<IReadOnlyList<string>> ListDirectoriesAsync(string path) =>
            Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}
