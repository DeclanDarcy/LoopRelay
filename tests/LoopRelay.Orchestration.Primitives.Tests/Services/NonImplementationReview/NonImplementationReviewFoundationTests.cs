using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Orchestration.Models.NonImplementationLedger;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Permissions.Models.Policy;

namespace LoopRelay.Orchestration.Tests.Services.NonImplementationReview;

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
        Assert.Equal(".agents/evidence", OrchestrationArtifactPaths.EvidenceDirectory);
        Assert.True(OrchestrationArtifactPaths.IsAgentsPath(".agents/review/non-implementation-ledger.json"));
        Assert.False(OrchestrationArtifactPaths.IsAgentsPath("docs/review.md"));
        Assert.Equal(".agents/review/non-implementation-ledger.json", OrchestrationArtifactPaths.NonImplementationLedger);
        Assert.Equal(".agents/review/non-implementation-review.md", OrchestrationArtifactPaths.NonImplementationReview);
        Assert.Equal(".agents/review/non-implementation-decisions.md", OrchestrationArtifactPaths.NonImplementationDecisions);
        Assert.Equal(".agents/review/non-implementation-synthesis.md", OrchestrationArtifactPaths.NonImplementationSynthesis);
        Assert.Equal(".agents/evidence/non-implementation", OrchestrationArtifactPaths.NonImplementationEvidenceDirectory);
    }

    [Fact]
    public void Prompt_evidence_sections_are_selected_from_canonical_review_paths()
    {
        IReadOnlyList<NonImplementationReviewPromptEvidenceSection> sections =
            NonImplementationReviewPromptEvidence.BuildSections(
            [
                " .agents/archive/epics/1/review/non-implementation-review.md ",
                OrchestrationArtifactPaths.NonImplementationReview,
                "",
            ]);

        Assert.Collection(
            sections,
            section =>
            {
                Assert.Equal("Non-Implementation Review Summary: .agents/archive/epics/1/review/non-implementation-review.md", section.Title);
                Assert.Equal(".agents/archive/epics/1/review/non-implementation-review.md", section.Path);
            },
            section =>
            {
                Assert.Equal($"Non-Implementation Review Summary: {OrchestrationArtifactPaths.NonImplementationReview}", section.Title);
                Assert.Equal(OrchestrationArtifactPaths.NonImplementationReview, section.Path);
            },
            section =>
            {
                Assert.Equal($"Non-Implementation Review Synthesis: {OrchestrationArtifactPaths.NonImplementationSynthesis}", section.Title);
                Assert.Equal(OrchestrationArtifactPaths.NonImplementationSynthesis, section.Path);
            });
    }

    [Fact]
    public void Prompt_policy_composer_defaults_to_implementation_first_mode()
    {
        string policy = ImplementationFirstPromptPolicyComposer.Compose(NonImplementationArtifactPolicyOptions.Default);

        Assert.Contains("Repository growth is implementation-first", policy, StringComparison.Ordinal);
        Assert.Contains("exception is disabled", policy, StringComparison.Ordinal);
        Assert.Contains("Never invent autonomous documentation", policy, StringComparison.Ordinal);
        Assert.Contains("Architecture Tests", policy, StringComparison.Ordinal);
        Assert.Contains("Golden Tests", policy, StringComparison.Ordinal);
        Assert.Contains("does not disable post-execution non-implementation review", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_policy_composer_preserves_hitl_requested_exception_when_enabled()
    {
        string policy = ImplementationFirstPromptPolicyComposer.Compose(
            new NonImplementationArtifactPolicyOptions(
                AllowHitlRequestedNonImplementationFiles: true,
                AllowAuxiliaryNonImplementationFiles: false));

        Assert.Contains("explicit HITL request evidence", policy, StringComparison.Ordinal);
        Assert.Contains("HITL-requested documentation exception", policy, StringComparison.Ordinal);
        Assert.Contains("never infer the exception", policy, StringComparison.Ordinal);
        Assert.Contains("Never invent autonomous documentation", policy, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_policy_section_is_rendered_from_central_composer()
    {
        string prompt = ImplementationFirstPromptPolicyComposer.AppendPromptPolicy(
            "do work",
            ImplementationFirstPromptPolicyComposer.ComposeDefault());

        Assert.Contains(ImplementationFirstPromptPolicyComposer.SectionHeading, prompt, StringComparison.Ordinal);
        Assert.Contains("do work", prompt, StringComparison.Ordinal);
        Assert.Contains("Repository growth is implementation-first", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_policy_body_is_not_hard_coded_outside_the_composer()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourceRoot = Path.Combine(repositoryRoot, "src");
        string composerPath = Path.GetFullPath(Path.Combine(
            sourceRoot,
            "LoopRelay.Orchestration.Primitives",
            "Services",
            "NonImplementationReview",
            "ImplementationFirstPromptPolicyComposer.cs"));
        string[] policyLines = ImplementationFirstPromptPolicyComposer.ComposeDefault()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string file in Directory.EnumerateFiles(sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".prompt", StringComparison.OrdinalIgnoreCase)))
        {
            string fullPath = Path.GetFullPath(file);
            if (string.Equals(fullPath, composerPath, StringComparison.OrdinalIgnoreCase) ||
                fullPath.Split(Path.DirectorySeparatorChar).Contains("bin") ||
                fullPath.Split(Path.DirectorySeparatorChar).Contains("obj"))
            {
                continue;
            }

            string content = File.ReadAllText(fullPath);
            foreach (string policyLine in policyLines)
            {
                Assert.DoesNotContain(policyLine, content, StringComparison.Ordinal);
            }
        }
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
        Assert.Contains("\"hitlRequests\": []", saved, StringComparison.Ordinal);

        NonImplementationReviewLedgerDocument loaded = await store.LoadOrCreateAsync();
        Assert.Equal(NonImplementationReviewLedgerDocument.CurrentSchemaVersion, loaded.SchemaVersion);
        Assert.Empty(loaded.Entries);
        Assert.Empty(loaded.HitlRequests);
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

    [Fact]
    public void Hitl_request_capture_ignores_unstructured_prose()
    {
        IReadOnlyList<NonImplementationHitlRequestEntry> requests =
            ExplicitHitlNonImplementationRequestCaptureService.ParseStructuredRequests(
                OrchestrationArtifactPaths.Plan,
                "Please create docs/requested.md for the human.",
                DateTimeOffset.UnixEpoch);

        Assert.Empty(requests);
    }

    [Fact]
    public async Task Hitl_request_capture_persists_only_structured_markers()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        var capture = new ExplicitHitlNonImplementationRequestCaptureService(store);
        const string source = """
            # Plan

            ## HITL-Requested Non-Implementation Deliverables

            | Path Or Pattern | Source | Source Hash | Rationale |
            | --- | --- | --- | --- |
            | docs/requested.md | user | abc | Human asked for the design note. |
            """;

        ExplicitHitlNonImplementationRequestCaptureResult result =
            await capture.CaptureFromSourceAsync(
                OrchestrationArtifactPaths.Plan,
                source,
                DateTimeOffset.UnixEpoch);
        ExplicitHitlNonImplementationRequestCaptureResult duplicate =
            await capture.CaptureFromSourceAsync(
                OrchestrationArtifactPaths.Plan,
                source,
                DateTimeOffset.UnixEpoch);

        Assert.Equal(1, result.CapturedCount);
        Assert.Equal(0, duplicate.CapturedCount);
        NonImplementationReviewLedgerDocument loaded = await store.LoadOrCreateAsync();
        NonImplementationHitlRequestEntry request = Assert.Single(loaded.HitlRequests);
        Assert.Equal("docs/requested.md", request.DeliverablePathOrPattern);
        Assert.Equal(OrchestrationArtifactPaths.Plan, request.SourceArtifactPath);
        Assert.Equal(NonImplementationHitlProvenanceKind.HitlRequested, request.HitlProvenanceKind);
        Assert.Equal("Human asked for the design note.", request.Rationale);
    }

    [Fact]
    public async Task Hitl_request_capture_attaches_matching_evidence_to_ledger_entries()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        await store.SaveAsync(new NonImplementationReviewLedgerDocument(
            NonImplementationReviewLedgerDocument.CurrentSchemaVersion,
            [
                new NonImplementationReviewLedgerEntry
                {
                    EntryId = "entry-1",
                    ExecutionSliceId = "slice-1",
                    Path = "docs/requested.md",
                    ReviewedContentSha256 = "reviewed-hash",
                    ReviewedFileDeleted = false,
                    Route = NonImplementationArtifactRoute.SemanticReviewCandidate,
                    ClassificationRuleId = "rule",
                    ClassificationRationale = "candidate",
                    ClassificationPathFacts = ["path=docs/requested.md"],
                    ClassifierVersion = "classifier",
                    SemanticDisposition = NonImplementationSemanticDisposition.ConfirmedNonImplementation,
                    SemanticRationale = "confirmed",
                    SemanticEvidence = ["evidence"],
                    ConfirmationPromptSourceHash = "prompt-hash",
                    FirstSeenAtUtc = DateTimeOffset.UnixEpoch,
                    LastSeenAtUtc = DateTimeOffset.UnixEpoch,
                    ResolutionState = NonImplementationResolutionState.Unresolved,
                    HitlProvenanceKind = NonImplementationHitlProvenanceKind.None,
                },
            ],
            [
                new NonImplementationHitlRequestEntry(
                    "docs/*.md",
                    OrchestrationArtifactPaths.Plan,
                    "source-hash",
                    NonImplementationHitlProvenanceKind.HitlRequested,
                    "Human requested the note.",
                    DateTimeOffset.UnixEpoch,
                    "| docs/*.md | Human requested the note. |"),
            ]));
        var capture = new ExplicitHitlNonImplementationRequestCaptureService(store);

        int attached = await capture.AttachRequestEvidenceAsync();

        Assert.Equal(1, attached);
        NonImplementationReviewLedgerEntry entry = Assert.Single((await store.LoadOrCreateAsync()).Entries);
        Assert.Equal(NonImplementationHitlProvenanceKind.HitlRequested, entry.HitlProvenanceKind);
        Assert.Equal(OrchestrationArtifactPaths.Plan, entry.HitlProvenanceEvidencePath);
        Assert.Equal("source-hash", entry.HitlProvenanceSourceHash);
        Assert.Equal("Human requested the note.", entry.HitlProvenanceRationale);
        Assert.Equal("docs/*.md", entry.HitlProvenanceEvidenceExcerpt?.Split('|')[1].Trim());
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

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
