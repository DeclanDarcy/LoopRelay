using System.Text.Json;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Orchestration.Tests.Services;

public sealed class NonImplementationInsightSynthesisTests
{
    private static readonly NonImplementationInsightSynthesizerOptions TestOptions =
        new("SynthesizeNonImplementationInsights", "synthesis-prompt-hash", 32768, 1024);

    [Fact]
    public async Task Synthesizer_is_not_invoked_when_no_confirmed_entries_exist()
    {
        var artifacts = new InMemoryArtifactStore();
        var ledger = new NonImplementationReviewLedgerStore(artifacts);
        await ledger.SaveAsync(NonImplementationReviewLedgerDocument.Empty() with
        {
            Entries =
            [
                Entry("ni-false", "docs/false.md", "hash-false", NonImplementationSemanticDisposition.FalsePositive),
                Entry("ni-uncertain", "docs/uncertain.md", "hash-uncertain", NonImplementationSemanticDisposition.Uncertain),
            ],
        });
        var runner = new RecordingReviewRunner(_ => throw new InvalidOperationException("Should not synthesize."));
        var synthesizer = new NonImplementationInsightSynthesizer(ledger, runner, artifacts, TestOptions);

        NonImplementationInsightSynthesisResult result = await synthesizer.SynthesizeAsync();

        Assert.True(result.SkippedNoConfirmedEntries);
        Assert.False(result.Generated);
        Assert.Empty(runner.Requests);
        Assert.Null(await artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationSynthesis));
    }

    [Fact]
    public async Task False_positives_are_excluded_and_uncertainties_are_separate()
    {
        var artifacts = new InMemoryArtifactStore();
        await artifacts.WriteAsync("docs/design.md", "# Design\n\nConfirmed prose.");
        await artifacts.WriteAsync("docs/uncertain.md", "# Maybe\n\nUnclear.");
        await artifacts.WriteAsync("docs/false.md", "# Release\n\nMachine metadata.");
        var ledger = new NonImplementationReviewLedgerStore(artifacts);
        await ledger.SaveAsync(NonImplementationReviewLedgerDocument.Empty() with
        {
            Entries =
            [
                Entry("ni-confirmed", "docs/design.md", "hash-confirmed", NonImplementationSemanticDisposition.ConfirmedNonImplementation),
                Entry("ni-false", "docs/false.md", "hash-false", NonImplementationSemanticDisposition.FalsePositive),
                Entry("ni-uncertain", "docs/uncertain.md", "hash-uncertain", NonImplementationSemanticDisposition.Uncertain),
            ],
        });
        var runner = new RecordingReviewRunner(request =>
        {
            using JsonDocument document = JsonDocument.Parse(ExtractInputPayload(request.PromptPayload));
            JsonElement root = document.RootElement;
            JsonElement confirmed = root.GetProperty("confirmedNonImplementationEntries");
            JsonElement uncertain = root.GetProperty("semanticUncertaintyEntries");

            Assert.Equal(1, confirmed.GetArrayLength());
            Assert.Equal("docs/design.md", confirmed[0].GetProperty("path").GetString());
            Assert.Equal(1, uncertain.GetArrayLength());
            Assert.Equal("docs/uncertain.md", uncertain[0].GetProperty("path").GetString());
            Assert.DoesNotContain("docs/false.md", request.PromptPayload, StringComparison.Ordinal);

            return new NonImplementationReviewRunnerResponse(
                "- Confirmed prose context from `docs/design.md` (`ni-confirmed`).\n\n" +
                "## Uncertain, Not Synthesized As Fact\n\n" +
                "- `docs/uncertain.md` (`ni-uncertain`) needs human review.");
        });
        var synthesizer = new NonImplementationInsightSynthesizer(ledger, runner, artifacts, TestOptions);

        NonImplementationInsightSynthesisResult result = await synthesizer.SynthesizeAsync();

        Assert.True(result.Generated);
        Assert.Equal(2, result.SourceEntries.Count);
        Assert.Contains(result.SourceEntries, source => source.EntryId == "ni-confirmed");
        Assert.Contains(result.SourceEntries, source => source.EntryId == "ni-uncertain");
    }

    [Fact]
    public async Task Synthesis_output_path_is_stable_and_source_linked()
    {
        var artifacts = new InMemoryArtifactStore();
        await artifacts.WriteAsync("docs/design.md", "# Design\n\nConfirmed prose.");
        var ledger = new NonImplementationReviewLedgerStore(artifacts);
        await ledger.SaveAsync(NonImplementationReviewLedgerDocument.Empty() with
        {
            Entries =
            [
                Entry("ni-confirmed", "docs/design.md", "hash-confirmed", NonImplementationSemanticDisposition.ConfirmedNonImplementation),
            ],
        });
        var runner = new RecordingReviewRunner(request =>
        {
            request.Constraints.EnsureReadOnly();
            Assert.Equal("SynthesizeNonImplementationInsights", request.PromptName);
            return new NonImplementationReviewRunnerResponse(
                "- Useful review context from `docs/design.md` (`ni-confirmed`).");
        });
        var synthesizer = new NonImplementationInsightSynthesizer(ledger, runner, artifacts, TestOptions);

        NonImplementationInsightSynthesisResult result = await synthesizer.SynthesizeAsync();
        string? synthesis = await artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationSynthesis);

        Assert.Equal(OrchestrationArtifactPaths.NonImplementationSynthesis, result.SynthesisPath);
        Assert.NotNull(synthesis);
        Assert.Contains("<!-- non-implementation-synthesis-metadata", synthesis, StringComparison.Ordinal);
        Assert.Contains("\"entryId\": \"ni-confirmed\"", synthesis, StringComparison.Ordinal);
        Assert.Contains("\"reviewedContentSha256\": \"hash-confirmed\"", synthesis, StringComparison.Ordinal);
        Assert.Contains("- `ni-confirmed` `docs/design.md` `sha256:hash-confirmed`", synthesis, StringComparison.Ordinal);
        Assert.Contains("does not authorize keeping, deleting", synthesis, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stale_source_entry_ids_hashes_or_prompt_hash_require_regeneration()
    {
        var artifacts = new InMemoryArtifactStore();
        await artifacts.WriteAsync("docs/design.md", "# Design\n\nVersion one.");
        var ledger = new NonImplementationReviewLedgerStore(artifacts);
        NonImplementationReviewLedgerEntry entry =
            Entry("ni-confirmed", "docs/design.md", "hash-a", NonImplementationSemanticDisposition.ConfirmedNonImplementation);
        await ledger.SaveAsync(NonImplementationReviewLedgerDocument.Empty() with { Entries = [entry] });
        int runCount = 0;
        var runner = new RecordingReviewRunner(_ =>
            new NonImplementationReviewRunnerResponse($"- Synthesis run {++runCount} from `docs/design.md` (`ni-confirmed`)."));
        var synthesizer = new NonImplementationInsightSynthesizer(ledger, runner, artifacts, TestOptions);

        NonImplementationInsightSynthesisResult first = await synthesizer.SynthesizeAsync();
        NonImplementationInsightSynthesisResult reused = await synthesizer.SynthesizeAsync();

        Assert.True(first.Generated);
        Assert.True(reused.ReusedExisting);
        Assert.Equal(1, runCount);

        await ledger.SaveAsync(NonImplementationReviewLedgerDocument.Empty() with
        {
            Entries = [entry with { ReviewedContentSha256 = "hash-b" }],
        });
        NonImplementationInsightSynthesisResult hashChanged = await synthesizer.SynthesizeAsync();

        await ledger.SaveAsync(NonImplementationReviewLedgerDocument.Empty() with
        {
            Entries = [entry with { EntryId = "ni-renewed", ReviewedContentSha256 = "hash-b" }],
        });
        NonImplementationInsightSynthesisResult idChanged = await synthesizer.SynthesizeAsync();

        var promptChanged = new NonImplementationInsightSynthesizer(
            ledger,
            runner,
            artifacts,
            TestOptions with { SynthesisPromptSourceHash = "synthesis-prompt-hash-v2" });
        NonImplementationInsightSynthesisResult promptHashChanged = await promptChanged.SynthesizeAsync();

        Assert.True(hashChanged.Generated);
        Assert.True(hashChanged.PreviousSynthesisWasStale);
        Assert.True(idChanged.Generated);
        Assert.True(idChanged.PreviousSynthesisWasStale);
        Assert.True(promptHashChanged.Generated);
        Assert.True(promptHashChanged.PreviousSynthesisWasStale);
        Assert.Equal(4, runCount);
    }

    [Fact]
    public void Synthesis_runner_must_be_read_only()
    {
        var artifacts = new InMemoryArtifactStore();
        var ledger = new NonImplementationReviewLedgerStore(artifacts);
        var runner = new RecordingReviewRunner(_ => throw new InvalidOperationException())
        {
            Capabilities = new NonImplementationReviewRunnerConstraints(
                allowsWorkspaceWrites: true,
                allowsCommits: false,
                allowsPushes: false,
                allowsMutationCapableScopedOperations: false),
        };

        Assert.Throws<InvalidOperationException>(
            () => new NonImplementationInsightSynthesizer(ledger, runner, artifacts, TestOptions));
    }

    private static string ExtractInputPayload(string prompt)
    {
        const string marker = "```json";
        int start = prompt.IndexOf(marker, StringComparison.Ordinal);
        int contentStart = prompt.IndexOf('\n', start);
        int end = prompt.IndexOf("```", contentStart + 1, StringComparison.Ordinal);
        return prompt[(contentStart + 1)..end].Trim();
    }

    private static NonImplementationReviewLedgerEntry Entry(
        string entryId,
        string path,
        string hash,
        NonImplementationSemanticDisposition disposition,
        NonImplementationResolutionState resolutionState = NonImplementationResolutionState.Unresolved) =>
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
            ClassificationPathFacts = [$"path={path}"],
            ClassifierVersion = NonImplementationArtifactClassifier.Version,
            SemanticDisposition = disposition,
            SemanticRationale = disposition == NonImplementationSemanticDisposition.Uncertain
                ? "Semantic uncertainty remains."
                : $"{disposition} rationale.",
            SemanticEvidence = [$"{disposition} evidence."],
            SemanticUncertaintyNote = disposition == NonImplementationSemanticDisposition.Uncertain
                ? "Needs human review."
                : null,
            ConfirmationPromptSourceHash = "confirmation-prompt-hash",
            FirstSeenAtUtc = DateTimeOffset.UnixEpoch,
            LastSeenAtUtc = DateTimeOffset.UnixEpoch,
            HitlProvenanceKind = NonImplementationHitlProvenanceKind.None,
            ResolutionState = resolutionState,
        };

    private sealed class RecordingReviewRunner(
        Func<NonImplementationReviewRunnerRequest, NonImplementationReviewRunnerResponse> handler)
        : INonImplementationReviewRunner
    {
        public NonImplementationReviewRunnerConstraints Capabilities { get; init; } =
            NonImplementationReviewRunnerConstraints.ReadOnly;

        public List<NonImplementationReviewRunnerRequest> Requests { get; } = [];

        public Task<NonImplementationReviewRunnerResponse> RunAsync(
            NonImplementationReviewRunnerRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(handler(request));
        }
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
