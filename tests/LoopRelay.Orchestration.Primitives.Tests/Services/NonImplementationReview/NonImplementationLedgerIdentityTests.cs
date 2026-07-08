using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationLedger;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Orchestration.Tests.Services.NonImplementationReview;

public sealed class NonImplementationLedgerIdentityTests
{
    [Fact]
    public async Task Upsert_pending_candidate_writes_schema_version_and_review_identity()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);

        NonImplementationReviewLedgerEntry entry = await store.UpsertPendingCandidateAsync(
            Candidate("docs/design.md", "hash-a"),
            "prompt-hash",
            DateTimeOffset.UnixEpoch,
            discoveryContext: "post-execution");

        string? json = await artifacts.ReadAsync(OrchestrationArtifactPaths.NonImplementationLedger);

        Assert.NotNull(json);
        Assert.StartsWith("ni-", entry.EntryId, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"executionSliceId\": \"slice-test\"", json, StringComparison.Ordinal);
        Assert.Contains("\"reviewedContentSha256\": \"hash-a\"", json, StringComparison.Ordinal);
        Assert.Contains("\"route\": \"SemanticReviewCandidate\"", json, StringComparison.Ordinal);
        Assert.Contains("\"confirmationPromptSourceHash\": \"prompt-hash\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ledger_queries_return_pending_confirmed_false_positive_and_uncertain_entries_separately()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        NonImplementationReviewLedgerEntry pending = await store.UpsertPendingCandidateAsync(
            Candidate("docs/pending.md", "hash-pending"),
            "prompt-hash",
            DateTimeOffset.UnixEpoch);
        NonImplementationReviewLedgerDocument document = await store.LoadOrCreateAsync();
        NonImplementationReviewLedgerEntry confirmed = EntryFrom(
            pending,
            "docs/confirmed.md",
            "hash-confirmed",
            NonImplementationSemanticDisposition.ConfirmedNonImplementation);
        NonImplementationReviewLedgerEntry falsePositive = EntryFrom(
            pending,
            "docs/false-positive.md",
            "hash-false-positive",
            NonImplementationSemanticDisposition.FalsePositive);
        NonImplementationReviewLedgerEntry uncertain = EntryFrom(
            pending,
            "docs/uncertain.md",
            "hash-uncertain",
            NonImplementationSemanticDisposition.Uncertain);
        await store.SaveAsync(document with { Entries = [pending, confirmed, falsePositive, uncertain] });

        IReadOnlyList<NonImplementationReviewLedgerEntry> pendingEntries =
            await store.LoadPendingSemanticConfirmationAsync();
        IReadOnlyList<NonImplementationReviewLedgerEntry> confirmedEntries =
            await store.LoadConfirmedNonImplementationAsync();
        IReadOnlyList<NonImplementationReviewLedgerEntry> falsePositives =
            await store.LoadFalsePositivesAsync();
        IReadOnlyList<NonImplementationReviewLedgerEntry> uncertainties =
            await store.LoadSemanticUncertaintiesAsync();

        Assert.Equal("docs/pending.md", Assert.Single(pendingEntries).Path);
        Assert.Equal("docs/confirmed.md", Assert.Single(confirmedEntries).Path);
        Assert.Equal("docs/false-positive.md", Assert.Single(falsePositives).Path);
        Assert.Equal("docs/uncertain.md", Assert.Single(uncertainties).Path);
    }

    [Fact]
    public async Task Same_path_hash_classifier_and_prompt_hash_suppresses_duplicate_confirmation()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        NonImplementationArtifactClassification candidate = Candidate("docs/design.md", "hash-a");
        NonImplementationReviewLedgerEntry pending = await store.UpsertPendingCandidateAsync(
            candidate,
            "prompt-hash",
            DateTimeOffset.UnixEpoch);
        NonImplementationReviewLedgerDocument document = await store.LoadOrCreateAsync();
        NonImplementationReviewLedgerEntry confirmed = pending with
        {
            SemanticDisposition = NonImplementationSemanticDisposition.ConfirmedNonImplementation,
            SemanticRationale = "It is a design note.",
            SemanticEvidence = ["prose-only design artifact"],
        };
        await store.SaveAsync(document with { Entries = [confirmed] });

        NonImplementationReviewLedgerEntry? reusable =
            await store.FindReusableSemanticDispositionAsync(candidate, "prompt-hash");

        Assert.NotNull(reusable);
        Assert.Equal(confirmed.EntryId, reusable.EntryId);
    }

    [Fact]
    public async Task Path_only_match_does_not_suppress_when_content_hash_changes()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        NonImplementationArtifactClassification original = Candidate("docs/design.md", "hash-a");
        NonImplementationReviewLedgerEntry pending = await store.UpsertPendingCandidateAsync(
            original,
            "prompt-hash",
            DateTimeOffset.UnixEpoch);
        NonImplementationReviewLedgerDocument document = await store.LoadOrCreateAsync();
        await store.SaveAsync(document with
        {
            Entries =
            [
                pending with
                {
                    SemanticDisposition = NonImplementationSemanticDisposition.ConfirmedNonImplementation,
                    SemanticRationale = "reviewed old content",
                    SemanticEvidence = ["old hash"],
                },
            ],
        });

        NonImplementationReviewLedgerEntry? reusable =
            await store.FindReusableSemanticDispositionAsync(Candidate("docs/design.md", "hash-b"), "prompt-hash");

        Assert.Null(reusable);
    }

    [Fact]
    public async Task Path_classifier_version_and_prompt_hash_changes_require_confirmation()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        NonImplementationArtifactClassification original = Candidate("docs/design.md", "hash-a");
        NonImplementationReviewLedgerEntry pending = await store.UpsertPendingCandidateAsync(
            original,
            "prompt-hash",
            DateTimeOffset.UnixEpoch);
        NonImplementationReviewLedgerDocument document = await store.LoadOrCreateAsync();
        await store.SaveAsync(document with
        {
            Entries =
            [
                pending with
                {
                    SemanticDisposition = NonImplementationSemanticDisposition.ConfirmedNonImplementation,
                    SemanticRationale = "reviewed original identity",
                    SemanticEvidence = ["identity evidence"],
                },
            ],
        });

        Assert.Null(await store.FindReusableSemanticDispositionAsync(
            Candidate("docs/renamed.md", "hash-a"),
            "prompt-hash"));
        Assert.Null(await store.FindReusableSemanticDispositionAsync(
            Candidate("docs/design.md", "hash-a", classifierVersion: "classifier/v2"),
            "prompt-hash"));
        Assert.Null(await store.FindReusableSemanticDispositionAsync(
            Candidate("docs/design.md", "hash-a"),
            "prompt-hash-v2"));
    }

    [Fact]
    public async Task Pending_entry_without_semantic_disposition_never_suppresses_confirmation()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        NonImplementationArtifactClassification candidate = Candidate("docs/design.md", "hash-a");
        await store.UpsertPendingCandidateAsync(candidate, "prompt-hash", DateTimeOffset.UnixEpoch);

        NonImplementationReviewLedgerEntry? reusable =
            await store.FindReusableSemanticDispositionAsync(candidate, "prompt-hash");

        Assert.Null(reusable);
    }

    [Fact]
    public async Task Deleted_file_identity_uses_deleted_marker_and_baseline_hash()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        NonImplementationArtifactClassification deleted =
            Candidate("docs/deleted.md", hash: null, deleted: true, baselineHash: "baseline-hash");
        NonImplementationReviewLedgerEntry pending = await store.UpsertPendingCandidateAsync(
            deleted,
            "prompt-hash",
            DateTimeOffset.UnixEpoch);
        NonImplementationReviewLedgerDocument document = await store.LoadOrCreateAsync();
        await store.SaveAsync(document with
        {
            Entries =
            [
                pending with
                {
                    SemanticDisposition = NonImplementationSemanticDisposition.ConfirmedNonImplementation,
                    SemanticRationale = "deleted reviewed prose",
                    SemanticEvidence = ["baseline hash"],
                },
            ],
        });

        NonImplementationReviewLedgerEntry? reusable =
            await store.FindReusableSemanticDispositionAsync(deleted, "prompt-hash");
        NonImplementationReviewLedgerEntry? changedDeletedIdentity =
            await store.FindReusableSemanticDispositionAsync(
                Candidate("docs/deleted.md", hash: null, deleted: true, baselineHash: "other-baseline-hash"),
                "prompt-hash");

        Assert.NotNull(reusable);
        Assert.True(reusable.ReviewedFileDeleted);
        Assert.Equal("baseline-hash", reusable.BaselineContentSha256);
        Assert.Null(changedDeletedIdentity);
    }

    [Fact]
    public async Task Hitl_keep_provenance_and_human_decision_metadata_are_durable()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        NonImplementationReviewLedgerEntry pending = await store.UpsertPendingCandidateAsync(
            Candidate("docs/design.md", "hash-a"),
            "prompt-hash",
            DateTimeOffset.UnixEpoch);
        await store.AttachHitlProvenanceAsync(
            pending.EntryId,
            NonImplementationHitlProvenanceKind.HitlKept,
            OrchestrationArtifactPaths.NonImplementationDecisions,
            "decision-source-hash",
            "Human kept the design note.",
            "keep docs/design.md");
        NonImplementationReviewLedgerDocument document = await store.LoadOrCreateAsync();
        NonImplementationReviewLedgerEntry withDecision = Assert.Single(document.Entries) with
        {
            ResolutionState = NonImplementationResolutionState.HitlKept,
            HumanDecision = new NonImplementationHumanDecisionMetadata(
                NonImplementationResolutionState.HitlKept,
                OrchestrationArtifactPaths.NonImplementationDecisions,
                "decision-source-hash",
                DateTimeOffset.UnixEpoch,
                Rationale: "Kept by human.",
                ReviewedContentSha256: "hash-a"),
        };
        await store.SaveAsync(document with { Entries = [withDecision] });

        NonImplementationReviewLedgerEntry loaded = Assert.Single((await store.LoadOrCreateAsync()).Entries);

        Assert.Equal(NonImplementationHitlProvenanceKind.HitlKept, loaded.HitlProvenanceKind);
        Assert.Equal(OrchestrationArtifactPaths.NonImplementationDecisions, loaded.HitlProvenanceEvidencePath);
        Assert.Equal("decision-source-hash", loaded.HitlProvenanceSourceHash);
        Assert.Equal("Human kept the design note.", loaded.HitlProvenanceRationale);
        Assert.Equal("keep docs/design.md", loaded.HitlProvenanceEvidenceExcerpt);
        Assert.Equal(NonImplementationResolutionState.HitlKept, loaded.HumanDecision?.ResolutionState);
    }

    private static NonImplementationReviewLedgerEntry EntryFrom(
        NonImplementationReviewLedgerEntry template,
        string path,
        string hash,
        NonImplementationSemanticDisposition disposition) =>
        template with
        {
            EntryId = $"entry-{disposition}",
            Path = path,
            ReviewedContentSha256 = hash,
            SemanticDisposition = disposition,
            SemanticRationale = $"{disposition} rationale",
            SemanticEvidence = [$"{disposition} evidence"],
        };

    private static NonImplementationArtifactClassification Candidate(
        string path,
        string? hash,
        bool deleted = false,
        string? baselineHash = null,
        string classifierVersion = NonImplementationArtifactClassifier.Version)
    {
        var file = new RepositoryChangedFileFacts(
            "slice-test",
            path.Replace('\\', '/'),
            PreviousPath: null,
            Status: deleted ? " D" : " M",
            BaselineStatus: baselineHash is null ? null : " M",
            PostStatus: deleted ? " D" : " M",
            PreExisted: baselineHash is not null,
            Exists: !deleted,
            IsDeleted: deleted,
            Extension: Path.GetExtension(path).ToLowerInvariant(),
            Size: deleted ? null : 1,
            BaselineContentSha256: baselineHash,
            PostContentSha256: deleted ? null : hash,
            TrackedDiffMetadata: Array.Empty<RepositoryGitDiffNameStatus>());

        return new NonImplementationArtifactClassification(
            file,
            NonImplementationArtifactRoute.SemanticReviewCandidate,
            "likely-prose-design-audit-roadmap-report",
            [$"path={path.Replace('\\', '/')}"],
            "The changed file needs semantic review.",
            classifierVersion);
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
