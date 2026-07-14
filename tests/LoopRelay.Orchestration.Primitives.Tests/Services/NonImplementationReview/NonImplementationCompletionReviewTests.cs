using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationCompletion;
using LoopRelay.Orchestration.Models.NonImplementationLedger;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationCompletion;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Services.RepositorySlices;

namespace LoopRelay.Orchestration.Tests.Services.NonImplementationReview;

public sealed class NonImplementationCompletionReviewTests
{
    private static readonly NonImplementationSemanticConfirmerOptions TestOptions =
        new("ConfirmNonImplementationCandidate", "confirmation-prompt-hash", 32768);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static NonImplementationCompletionReviewTests()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public async Task Completion_review_returns_ready_after_fresh_scan_when_no_blocking_entries_exist()
    {
        using TempReviewRepository temp = TempReviewRepository.Create();
        var git = new FakeProcessRunner { StatusOutput = string.Empty };
        NonImplementationCompletionReviewService service = temp.NewService(git, ConfirmingRunner());

        NonImplementationCompletionReviewResult result = await service.ReviewAsync();

        Assert.True(result.IsReady);
        Assert.Equal(0, result.Summary.CurrentChangedFileCount);
        Assert.NotNull(await temp.Artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationReview));
    }

    [Fact]
    public async Task Completion_review_fresh_scan_blocks_when_current_prose_is_not_in_ledger()
    {
        using TempReviewRepository temp = TempReviewRepository.Create();
        await temp.WriteFileAsync("docs/report.md", "# Report\n\nReview-only note.");
        var git = new FakeProcessRunner { StatusOutput = "?? docs/report.md" };
        NonImplementationCompletionReviewService service = temp.NewService(git, ConfirmingRunner());

        NonImplementationCompletionReviewResult result = await service.ReviewAsync();

        Assert.True(result.IsBlocked);
        Assert.Equal(1, result.Summary.CurrentChangedFileCount);
        Assert.Equal(1, result.Summary.UnresolvedConfirmedNonImplementationCount);
        Assert.NotNull(await temp.Artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationDecisions));
        NonImplementationReviewLedgerEntry entry = Assert.Single((await temp.Ledger.LoadOrCreateAsync()).Entries);
        Assert.Equal("docs/report.md", entry.Path);
        Assert.Equal(NonImplementationSemanticDisposition.ConfirmedNonImplementation, entry.SemanticDisposition);
    }

    [Fact]
    public async Task Completion_review_blocks_when_unresolved_confirmed_entry_has_no_decisions_file()
    {
        using TempReviewRepository temp = TempReviewRepository.Create();
        await temp.WriteFileAsync("docs/design.md", "# Design");
        await temp.SaveLedgerEntryAsync(Entry("ni-design", "docs/design.md", await temp.FileSha256Async("docs/design.md")));
        NonImplementationCompletionReviewService service = temp.NewService(new FakeProcessRunner(), ConfirmingRunner());

        NonImplementationCompletionReviewResult result = await service.ReviewAsync();

        Assert.True(result.IsBlocked);
        string? template = await temp.Artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationDecisions);
        Assert.Contains("| ni-design | docs/design.md |", template);
    }

    [Fact]
    public async Task Keep_decision_records_hitl_keep_or_request_evidence_and_allows_completion()
    {
        using TempReviewRepository temp = TempReviewRepository.Create();
        await temp.WriteFileAsync("docs/design.md", "# Design");
        string hash = await temp.FileSha256Async("docs/design.md");
        await temp.SaveLedgerEntryAsync(Entry("ni-design", "docs/design.md", hash));
        await temp.WriteDecisionsAsync(FileDecisionRow("ni-design", "docs/design.md", hash, "ConfirmedNonImplementation", "Keep", "HitlRequested: user asked for this."));
        NonImplementationCompletionReviewService service = temp.NewService(new FakeProcessRunner(), ConfirmingRunner());

        NonImplementationCompletionReviewResult result = await service.ReviewAsync();

        Assert.True(result.IsReady);
        NonImplementationReviewLedgerEntry entry = Assert.Single((await temp.Ledger.LoadOrCreateAsync()).Entries);
        Assert.Equal(NonImplementationResolutionState.HitlKept, entry.ResolutionState);
        Assert.Equal(NonImplementationHitlProvenanceKind.HitlRequested, entry.HitlProvenanceKind);
        Assert.Equal(OrchestrationArtifactPaths.NonImplementationDecisions, entry.HitlProvenanceEvidencePath);
    }

    [Fact]
    public async Task Delete_decision_removes_only_reviewed_matching_repository_file()
    {
        using TempReviewRepository temp = TempReviewRepository.Create();
        await temp.WriteFileAsync("docs/remove.md", "# Remove");
        string hash = await temp.FileSha256Async("docs/remove.md");
        await temp.SaveLedgerEntryAsync(Entry("ni-remove", "docs/remove.md", hash));
        await temp.WriteDecisionsAsync(FileDecisionRow("ni-remove", "docs/remove.md", hash, "ConfirmedNonImplementation", "Delete", "Remove it."));
        NonImplementationCompletionReviewService service = temp.NewService(new FakeProcessRunner(), ConfirmingRunner());

        NonImplementationCompletionReviewResult result = await service.ReviewAsync();

        Assert.True(result.IsReady);
        Assert.Equal(["docs/remove.md"], result.AppliedDeletePaths);
        Assert.False(File.Exists(temp.Resolve("docs/remove.md")));
        NonImplementationReviewLedgerEntry entry = Assert.Single((await temp.Ledger.LoadOrCreateAsync()).Entries);
        Assert.Equal(NonImplementationResolutionState.HitlDeleted, entry.ResolutionState);
    }

    [Fact]
    public async Task Stale_delete_blocks_when_current_hash_changed_after_review()
    {
        using TempReviewRepository temp = TempReviewRepository.Create();
        await temp.WriteFileAsync("docs/remove.md", "# Current");
        await temp.SaveLedgerEntryAsync(Entry("ni-remove", "docs/remove.md", "reviewed-old-hash"));
        await temp.WriteDecisionsAsync(FileDecisionRow("ni-remove", "docs/remove.md", "reviewed-old-hash", "ConfirmedNonImplementation", "Delete", "Remove it."));
        NonImplementationCompletionReviewService service = temp.NewService(new FakeProcessRunner(), ConfirmingRunner());

        NonImplementationCompletionReviewResult result = await service.ReviewAsync();

        Assert.True(result.IsBlocked);
        Assert.True(File.Exists(temp.Resolve("docs/remove.md")));
        Assert.Contains(result.UnresolvedMessages, message => message.Contains("hash changed", StringComparison.Ordinal));
        NonImplementationReviewLedgerEntry entry = Assert.Single((await temp.Ledger.LoadOrCreateAsync()).Entries);
        Assert.Equal(NonImplementationResolutionState.Unresolved, entry.ResolutionState);
    }

    [Fact]
    public async Task Delete_decision_rejects_path_traversal_and_agents_paths()
    {
        using TempReviewRepository temp = TempReviewRepository.Create();
        await temp.SaveLedgerAsync(
            Entry("ni-traversal", "../outside.md", "hash-outside"),
            Entry("ni-agents", ".agents/review/note.md", "hash-agents"));
        await temp.WriteDecisionsAsync(
            FileDecisionRow("ni-traversal", "../outside.md", "hash-outside", "ConfirmedNonImplementation", "Delete", "Bad path."),
            FileDecisionRow("ni-agents", ".agents/review/note.md", "hash-agents", "ConfirmedNonImplementation", "Delete", "Agents path."));
        NonImplementationCompletionReviewService service = temp.NewService(new FakeProcessRunner(), ConfirmingRunner());

        NonImplementationCompletionReviewResult result = await service.ReviewAsync();

        Assert.True(result.IsBlocked);
        Assert.Contains(result.UnresolvedMessages, message => message.Contains("path traversal", StringComparison.Ordinal));
        Assert.Contains(result.UnresolvedMessages, message => message.Contains(".agents", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(NonImplementationSynthesisDecision.KeepSynthesis)]
    [InlineData(NonImplementationSynthesisDecision.DiscardSynthesis)]
    [InlineData(NonImplementationSynthesisDecision.DeferSynthesis)]
    public async Task Synthesis_decisions_are_recorded_separately_from_file_decisions(
        NonImplementationSynthesisDecision synthesisDecision)
    {
        using TempReviewRepository temp = TempReviewRepository.Create();
        await temp.WriteFileAsync("docs/design.md", "# Design");
        await temp.Artifacts.WriteAsync(OrchestrationArtifactPaths.NonImplementationSynthesis, "- Synthesized context.");
        string hash = await temp.FileSha256Async("docs/design.md");
        await temp.SaveLedgerEntryAsync(Entry("ni-design", "docs/design.md", hash));
        await temp.WriteDecisionsAsync(
            [FileDecisionRow("ni-design", "docs/design.md", hash, "ConfirmedNonImplementation", "Keep", "Keep file.")],
            synthesisDecision: synthesisDecision.ToString());
        NonImplementationCompletionReviewService service = temp.NewService(new FakeProcessRunner(), ConfirmingRunner());

        NonImplementationCompletionReviewResult result = await service.ReviewAsync();

        Assert.True(result.IsReady);
        NonImplementationReviewLedgerDocument ledger = await temp.Ledger.LoadOrCreateAsync();
        Assert.Equal(NonImplementationResolutionState.HitlKept, Assert.Single(ledger.Entries).ResolutionState);
        Assert.Equal(synthesisDecision, ledger.SynthesisDecision?.Decision);
        Assert.Equal(OrchestrationArtifactPaths.NonImplementationSynthesis, ledger.SynthesisDecision?.SynthesisPath);
    }

    [Theory]
    [InlineData("Keep", NonImplementationResolutionState.HitlKept, false)]
    [InlineData("Delete", NonImplementationResolutionState.HitlDeleted, true)]
    [InlineData("ResolveFalsePositive", NonImplementationResolutionState.HitlFalsePositive, false)]
    [InlineData("Defer", NonImplementationResolutionState.HitlDeferred, false)]
    public async Task Semantically_uncertain_entry_can_be_resolved_by_any_file_decision(
        string decision,
        NonImplementationResolutionState expectedResolution,
        bool deleted)
    {
        using TempReviewRepository temp = TempReviewRepository.Create();
        await temp.WriteFileAsync("notes/maybe.custom", "Maybe useful.");
        string hash = await temp.FileSha256Async("notes/maybe.custom");
        await temp.SaveLedgerEntryAsync(Entry(
            "ni-maybe",
            "notes/maybe.custom",
            hash,
            NonImplementationSemanticDisposition.Uncertain));
        await temp.WriteDecisionsAsync(FileDecisionRow("ni-maybe", "notes/maybe.custom", hash, "Uncertain", decision, "Human resolved uncertainty."));
        NonImplementationCompletionReviewService service = temp.NewService(new FakeProcessRunner(), ConfirmingRunner());

        NonImplementationCompletionReviewResult result = await service.ReviewAsync();

        Assert.True(result.IsReady);
        Assert.Equal(deleted, !File.Exists(temp.Resolve("notes/maybe.custom")));
        NonImplementationReviewLedgerEntry entry = Assert.Single((await temp.Ledger.LoadOrCreateAsync()).Entries);
        Assert.Equal(expectedResolution, entry.ResolutionState);
    }

    private static NonImplementationReviewLedgerEntry Entry(
        string entryId,
        string path,
        string hash,
        NonImplementationSemanticDisposition disposition = NonImplementationSemanticDisposition.ConfirmedNonImplementation) =>
        new()
        {
            EntryId = entryId,
            ExecutionSliceId = "slice-test",
            Path = path,
            ReviewedContentSha256 = hash,
            ReviewedFileDeleted = false,
            Route = NonImplementationArtifactRoute.SemanticReviewCandidate,
            ClassificationRuleId = "likely-prose-design-audit-roadmap-report",
            ClassificationRationale = "The changed file needs semantic review.",
            ClassificationPathFacts = [$"path={path}", $"postSha256={hash}"],
            ClassifierVersion = NonImplementationArtifactClassifier.Version,
            SemanticDisposition = disposition,
            SemanticRationale = disposition == NonImplementationSemanticDisposition.Uncertain
                ? "Uncertain."
                : "Confirmed.",
            SemanticEvidence = ["semantic evidence"],
            SemanticUncertaintyNote = disposition == NonImplementationSemanticDisposition.Uncertain
                ? "Needs human decision."
                : null,
            ConfirmationPromptSourceHash = TestOptions.ConfirmationPromptSourceHash,
            FirstSeenAtUtc = DateTimeOffset.UnixEpoch,
            LastSeenAtUtc = DateTimeOffset.UnixEpoch,
            HitlProvenanceKind = NonImplementationHitlProvenanceKind.None,
            ResolutionState = NonImplementationResolutionState.Unresolved,
        };

    private static string FileDecisionRow(
        string entryId,
        string path,
        string hash,
        string status,
        string decision,
        string reason) =>
        $"| {entryId} | {path} | {hash} | {status} | {decision} | {reason} |";

    private static RecordingReviewRunner ConfirmingRunner() =>
        new(request =>
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
                disposition = NonImplementationSemanticDisposition.ConfirmedNonImplementation,
                rationale = "Confirmed non-implementation prose.",
                evidenceExcerptsOrPathFacts = new[] { "path fact" },
                uncertaintyNote = (string?)null,
            }, JsonOptions);
            return new NonImplementationReviewRunnerResponse(json);
        });

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

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public string StatusOutput { get; init; } = string.Empty;

        public string DiffOutput { get; init; } = string.Empty;

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory)
        {
            string output = arguments[0] == "status" ? StatusOutput : DiffOutput;
            return Task.FromResult(new ProcessRunResult
            {
                ExitCode = 0,
                StandardOutput = output,
                StandardError = string.Empty,
                Duration = TimeSpan.Zero,
            });
        }

        public Task<IAgentProcess> StartInteractiveAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            string workingDirectory,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TempReviewRepository : IDisposable
    {
        private readonly FileSystemArtifactStore store = new();

        private TempReviewRepository(string root)
        {
            Root = root;
            Repository = new Repository { Id = Guid.NewGuid(), Name = "repo", Path = root };
            Artifacts = new TestRepositoryArtifactStore(store, Repository);
            Ledger = new NonImplementationReviewLedgerStore(Artifacts);
        }

        public string Root { get; }

        public Repository Repository { get; }

        public IArtifactStore Artifacts { get; }

        public NonImplementationReviewLedgerStore Ledger { get; }

        public static TempReviewRepository Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "looprelay-ni-completion", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TempReviewRepository(root);
        }

        public NonImplementationCompletionReviewService NewService(
            FakeProcessRunner git,
            INonImplementationReviewRunner runner)
        {
            var confirmer = new NonImplementationSemanticConfirmer(Ledger, runner, TestOptions);
            return new NonImplementationCompletionReviewService(
                new RepositoryChangeSetDetector(git, Repository),
                new NonImplementationArtifactClassifier(),
                confirmer,
                Ledger,
                Artifacts,
                Repository.Path);
        }

        public async Task WriteFileAsync(string relativePath, string content)
        {
            string fullPath = Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
        }

        public async Task<string> FileSha256Async(string relativePath)
        {
            await using FileStream stream = new(Resolve(relativePath), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] hash = await SHA256.HashDataAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public async Task SaveLedgerEntryAsync(NonImplementationReviewLedgerEntry entry) =>
            await SaveLedgerAsync(entry);

        public async Task SaveLedgerAsync(params NonImplementationReviewLedgerEntry[] entries) =>
            await Ledger.SaveAsync(NonImplementationReviewLedgerDocument.Empty() with { Entries = entries });

        public async Task WriteDecisionsAsync(params string[] rows) =>
            await WriteDecisionsAsync(rows, synthesisDecision: null);

        public async Task WriteDecisionsAsync(IReadOnlyList<string> rows, string? synthesisDecision)
        {
            var lines = new List<string>
            {
                "# Non-Implementation Review Decisions",
                string.Empty,
                "| Entry ID | Path | Reviewed SHA-256 | Reviewed Status | Decision | HITL Reason |",
                "|---|---|---|---|---|---|",
            };
            lines.AddRange(rows);
            if (synthesisDecision is not null)
            {
                lines.Add(string.Empty);
                lines.Add("## Synthesis Decision");
                lines.Add(string.Empty);
                lines.Add("| Synthesis Path | Decision | HITL Reason |");
                lines.Add("|---|---|---|");
                lines.Add($"| {OrchestrationArtifactPaths.NonImplementationSynthesis} | {synthesisDecision} | Human synthesis decision. |");
            }

            await Artifacts.WriteAsync(
                OrchestrationArtifactPaths.NonImplementationDecisions,
                string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }

        public string Resolve(string relativePath) =>
            Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class TestRepositoryArtifactStore(IArtifactStore store, Repository repository) : IArtifactStore
    {
        public Task<bool> ExistsAsync(string relativePath) =>
            store.ExistsAsync(Resolve(relativePath));

        public Task<string?> ReadAsync(string relativePath) =>
            store.ReadAsync(Resolve(relativePath));

        public Task WriteAsync(string relativePath, string content) =>
            store.WriteAsync(Resolve(relativePath), content);

        public Task DeleteAsync(string relativePath) =>
            store.DeleteAsync(Resolve(relativePath));

        public async Task<IReadOnlyList<string>> ListAsync(string relativeDirectory, string searchPattern)
        {
            IReadOnlyList<string> absolute = await store.ListAsync(Resolve(relativeDirectory), searchPattern);
            return absolute
                .Select(path => ArtifactPath.ToRepositoryRelativePath(repository, path))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public async Task<IReadOnlyList<string>> ListDirectoriesAsync(string relativeDirectory)
        {
            IReadOnlyList<string> absolute = await store.ListDirectoriesAsync(Resolve(relativeDirectory));
            return absolute
                .Select(path => ArtifactPath.ToRepositoryRelativePath(repository, path))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private string Resolve(string relativePath) =>
            ArtifactPath.ResolveRepositoryPath(repository, relativePath);
    }
}
