using LoopRelay.Core.Artifacts;
using LoopRelay.Orchestration;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Models;

namespace LoopRelay.Orchestration.Primitives.Tests;

public sealed class NonImplementationReviewFoundationTests
{
    [Fact]
    public void Vocabulary_keeps_deterministic_routes_distinct_from_semantic_dispositions()
    {
        Assert.Equal("AmbiguousForSemanticReview", nameof(NonImplementationArtifactRoute.AmbiguousForSemanticReview));
        Assert.Equal("Uncertain", nameof(NonImplementationSemanticDisposition.Uncertain));
        Assert.DoesNotContain("UncertainCandidate", Enum.GetNames<NonImplementationArtifactRoute>());
        Assert.Equal("HITL-requested non-implementation file", NonImplementationReviewTerms.HitlRequestedNonImplementationFile);
    }

    [Fact]
    public void Canonical_review_paths_are_owned_by_orchestration_paths()
    {
        Assert.Equal(".agents/review/non-implementation-ledger.json", OrchestrationArtifactPaths.NonImplementationLedger);
        Assert.Equal(".agents/review/non-implementation-review.md", OrchestrationArtifactPaths.NonImplementationReview);
        Assert.Equal(".agents/review/non-implementation-decisions.md", OrchestrationArtifactPaths.NonImplementationDecisions);
        Assert.Equal(".agents/review/non-implementation-synthesis.md", OrchestrationArtifactPaths.NonImplementationSynthesis);
        Assert.Equal(".agents/evidence/non-implementation", OrchestrationArtifactPaths.NonImplementationEvidenceDirectory);
    }

    [Fact]
    public void Prompt_policy_composer_defaults_to_implementation_first_mode()
    {
        string policy = ImplementationFirstPromptPolicyComposer.Compose(NonImplementationArtifactPolicyOptions.Default);

        Assert.Contains("Repository growth is implementation-first", policy, StringComparison.Ordinal);
        Assert.Contains("exception is disabled", policy, StringComparison.Ordinal);
        Assert.Contains("Never invent autonomous documentation", policy, StringComparison.Ordinal);
        Assert.Contains("does not disable post-execution non-implementation review", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_policy_composer_preserves_hitl_requested_exception_when_enabled()
    {
        string policy = ImplementationFirstPromptPolicyComposer.Compose(
            new NonImplementationArtifactPolicyOptions(AllowHitlRequestedNonImplementationFiles: true));

        Assert.Contains("explicit HITL request evidence", policy, StringComparison.Ordinal);
        Assert.Contains("Never invent autonomous documentation", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Review_runner_request_is_bounded_and_read_only()
    {
        var request = new NonImplementationReviewRunnerRequest(
            promptName: "ConfirmNonImplementationCandidate",
            promptPayload: "{}",
            maxPromptPayloadCharacters: 10);

        request.Constraints.EnsureReadOnly();
        Assert.False(request.Constraints.AllowsWorkspaceWrites);
        Assert.False(request.Constraints.AllowsCommits);
        Assert.False(request.Constraints.AllowsPushes);
        Assert.False(request.Constraints.AllowsMutationCapableScopedOperations);
    }

    [Fact]
    public void Review_runner_request_rejects_unbounded_payloads()
    {
        Assert.Throws<ArgumentException>(
            () => new NonImplementationReviewRunnerRequest("prompt", "too long", maxPromptPayloadCharacters: 3));
    }

    [Fact]
    public async Task Ledger_store_returns_empty_document_when_absent_and_writes_stable_path()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);

        NonImplementationReviewLedgerDocument empty = await store.LoadOrCreateAsync();
        await store.SaveAsync(empty);

        string? saved = await artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationLedger);
        Assert.NotNull(saved);
        Assert.Contains("\"schemaVersion\": 1", saved, StringComparison.Ordinal);
        Assert.Contains("\"entries\": []", saved, StringComparison.Ordinal);

        NonImplementationReviewLedgerDocument loaded = await store.LoadOrCreateAsync();
        Assert.Equal(NonImplementationReviewLedgerDocument.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Empty(loaded.Entries);
    }

    [Fact]
    public async Task Ledger_store_rejects_invalid_schema_version()
    {
        var artifacts = new InMemoryArtifactStore();
        await artifacts.WriteAsync(
            OrchestrationArtifactPaths.NonImplementationLedger,
            "{\"schemaVersion\":999,\"entries\":[]}");
        var store = new NonImplementationReviewLedgerStore(artifacts);

        NonImplementationReviewLedgerException exception =
            await Assert.ThrowsAsync<NonImplementationReviewLedgerException>(() => store.LoadOrCreateAsync());

        Assert.Contains("Unsupported non-implementation review ledger schema version", exception.Message);
    }

    private sealed class InMemoryArtifactStore : IArtifactStore
    {
        private readonly Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);

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
