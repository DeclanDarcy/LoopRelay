using System.Text.Json;
using System.Text.Json.Serialization;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Abstractions.Artifacts;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationLedger;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationSemanticConfirmation;
using LoopRelay.Orchestration.Models.RepositorySlices;
using LoopRelay.Orchestration.Primitives.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationLedger;
using LoopRelay.Orchestration.Services.NonImplementationReview;
using LoopRelay.Orchestration.Services.NonImplementationSemanticConfirmation;

namespace LoopRelay.Orchestration.Tests.Services.NonImplementationReview;

public sealed class NonImplementationSemanticConfirmationTests
{
    private static readonly NonImplementationSemanticConfirmerOptions TestOptions =
        new("ConfirmNonImplementationCandidate", "prompt-hash", 32768);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static NonImplementationSemanticConfirmationTests()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    [Theory]
    [InlineData(NonImplementationSemanticDisposition.ConfirmedNonImplementation)]
    [InlineData(NonImplementationSemanticDisposition.FalsePositive)]
    [InlineData(NonImplementationSemanticDisposition.Uncertain)]
    public void Parser_accepts_each_valid_disposition(NonImplementationSemanticDisposition disposition)
    {
        NonImplementationReviewLedgerEntry entry = Entry();

        NonImplementationSemanticConfirmation parsed =
            NonImplementationSemanticConfirmationParser.ParseAndValidate(
                ConfirmationJson(entry, disposition),
                entry);

        Assert.Equal(disposition, parsed.Disposition);
        Assert.Equal(entry.EntryId, parsed.LedgerEntryId);
        Assert.Equal(entry.Path, parsed.CandidatePath);
        Assert.Equal(entry.ReviewedContentSha256, parsed.ReviewedContentSha256);
        if (disposition == NonImplementationSemanticDisposition.Uncertain)
        {
            Assert.Equal("Needs more context.", parsed.UncertaintyNote);
        }
    }

    [Fact]
    public void Parser_rejects_missing_and_unknown_disposition()
    {
        NonImplementationReviewLedgerEntry entry = Entry();
        string missing = JsonSerializer.Serialize(new
        {
            ledgerEntryId = entry.EntryId,
            candidatePath = entry.Path,
            reviewedContentSha256 = entry.ReviewedContentSha256,
            reviewedFileDeleted = false,
            deletedReviewedIdentity = (string?)null,
            rationale = "rationale",
            evidenceExcerptsOrPathFacts = new[] { "path fact" },
        }, JsonOptions);
        string unknown = """
            {
              "ledgerEntryId": "ni-test",
              "candidatePath": "docs/design.md",
              "reviewedContentSha256": "hash-a",
              "reviewedFileDeleted": false,
              "deletedReviewedIdentity": null,
              "disposition": "MaybeDocumentation",
              "rationale": "rationale",
              "evidenceExcerptsOrPathFacts": ["path fact"],
              "uncertaintyNote": null
            }
            """;

        Assert.Throws<NonImplementationSemanticConfirmationParseException>(
            () => NonImplementationSemanticConfirmationParser.ParseAndValidate(missing, entry));
        Assert.Throws<NonImplementationSemanticConfirmationParseException>(
            () => NonImplementationSemanticConfirmationParser.ParseAndValidate(unknown, entry));
    }

    [Fact]
    public void Parser_rejects_malformed_non_json_output()
    {
        NonImplementationReviewLedgerEntry entry = Entry();

        Assert.Throws<NonImplementationSemanticConfirmationParseException>(
            () => NonImplementationSemanticConfirmationParser.ParseAndValidate(
                "```json\n{}\n```",
                entry));
    }

    [Fact]
    public void Parser_rejects_mismatched_entry_path_content_hash_or_reviewed_status()
    {
        NonImplementationReviewLedgerEntry entry = Entry();
        NonImplementationReviewLedgerEntry deleted = Entry(
            hash: null,
            deleted: true,
            baselineHash: "baseline-hash");

        Assert.Throws<NonImplementationSemanticConfirmationParseException>(
            () => NonImplementationSemanticConfirmationParser.ParseAndValidate(
                ConfirmationJson(entry),
                entry with { EntryId = "other-entry" }));
        Assert.Throws<NonImplementationSemanticConfirmationParseException>(
            () => NonImplementationSemanticConfirmationParser.ParseAndValidate(
                ConfirmationJson(entry),
                entry with { Path = "docs/other.md" }));
        Assert.Throws<NonImplementationSemanticConfirmationParseException>(
            () => NonImplementationSemanticConfirmationParser.ParseAndValidate(
                ConfirmationJson(entry, reviewedContentSha256: "other-hash"),
                entry));
        Assert.Throws<NonImplementationSemanticConfirmationParseException>(
            () => NonImplementationSemanticConfirmationParser.ParseAndValidate(
                ConfirmationJson(deleted, reviewedFileDeleted: false),
                deleted));
        Assert.Throws<NonImplementationSemanticConfirmationParseException>(
            () => NonImplementationSemanticConfirmationParser.ParseAndValidate(
                ConfirmationJson(deleted, deletedReviewedIdentity: "deleted:other-hash"),
                deleted));
    }

    [Fact]
    public async Task Service_skips_only_valid_exact_ledger_identities()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        NonImplementationArtifactClassification existingCandidate = Candidate("docs/design.md", "hash-a");
        NonImplementationReviewLedgerEntry existing =
            await store.UpsertPendingCandidateAsync(
                existingCandidate,
                TestOptions.ConfirmationPromptSourceHash,
                DateTimeOffset.UnixEpoch);
        NonImplementationReviewLedgerDocument document = await store.LoadOrCreateAsync();
        await store.SaveAsync(document with
        {
            Entries =
            [
                existing with
                {
                    SemanticDisposition = NonImplementationSemanticDisposition.ConfirmedNonImplementation,
                    SemanticRationale = "previously confirmed",
                    SemanticEvidence = ["previous hash"],
                },
            ],
        });
        var runner = new RecordingReviewRunner(request =>
            ResponseFromRequest(request, NonImplementationSemanticDisposition.FalsePositive));
        var confirmer = new NonImplementationSemanticConfirmer(store, runner, TestOptions);

        NonImplementationSemanticConfirmationBatchResult result =
            await confirmer.ConfirmAsync(
                new NonImplementationArtifactClassificationSet(
                    "slice-test",
                    [
                        existingCandidate,
                        Candidate("docs/design.md", "hash-b"),
                    ]),
                DateTimeOffset.UnixEpoch);

        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1, result.ConfirmedCount);
        Assert.Single(runner.Requests);
        Assert.Equal("hash-b", Assert.Single(result.ConfirmedEntries).ReviewedContentSha256);
    }

    [Fact]
    public async Task Service_confirms_candidates_and_records_rationale()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        var runner = new RecordingReviewRunner(request =>
            ResponseFromRequest(
                request,
                NonImplementationSemanticDisposition.ConfirmedNonImplementation,
                rationale: "It is a standalone design note.",
                evidence: ["Markdown design-only content."]));
        var confirmer = new NonImplementationSemanticConfirmer(store, runner, TestOptions);

        NonImplementationSemanticConfirmationBatchResult result =
            await confirmer.ConfirmAsync(
                new NonImplementationArtifactClassificationSet(
                    "slice-test",
                    [Candidate("docs/design.md", "hash-a")]),
                DateTimeOffset.UnixEpoch,
                discoveryContext: "post-execution");

        NonImplementationReviewLedgerEntry entry = Assert.Single(result.ConfirmedEntries);
        Assert.Equal(NonImplementationSemanticDisposition.ConfirmedNonImplementation, entry.SemanticDisposition);
        Assert.Equal("It is a standalone design note.", entry.SemanticRationale);
        Assert.Equal(["Markdown design-only content."], entry.SemanticEvidence);
        Assert.Equal(NonImplementationResolutionState.Unresolved, entry.ResolutionState);
        Assert.Null(entry.HumanDecision);
        NonImplementationReviewRunnerRequest request = Assert.Single(runner.Requests);
        request.Constraints.EnsureReadOnly();
        Assert.Equal("ConfirmNonImplementationCandidate", request.PromptName);
        Assert.Contains("\"ledgerEntryId\"", request.PromptPayload, StringComparison.Ordinal);
        Assert.Contains("Do not decide whether to keep, delete", request.PromptPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Service_treats_false_positive_as_normal_outcome()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        var runner = new RecordingReviewRunner(request =>
            ResponseFromRequest(
                request,
                NonImplementationSemanticDisposition.FalsePositive,
                rationale: "The file is machine-required release metadata.",
                evidence: ["path fact"]));
        var confirmer = new NonImplementationSemanticConfirmer(store, runner, TestOptions);

        NonImplementationSemanticConfirmationBatchResult result =
            await confirmer.ConfirmAsync(
                new NonImplementationArtifactClassificationSet(
                    "slice-test",
                    [Candidate("RELEASE_NOTES.txt", "hash-a")]),
                DateTimeOffset.UnixEpoch);

        Assert.Equal(NonImplementationSemanticDisposition.FalsePositive, Assert.Single(result.ConfirmedEntries).SemanticDisposition);
    }

    [Fact]
    public async Task Service_preserves_semantic_uncertainty_for_ambiguous_routes()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        var runner = new RecordingReviewRunner(request =>
            ResponseFromRequest(
                request,
                NonImplementationSemanticDisposition.Uncertain,
                rationale: "The extension is unknown and content evidence is inconclusive.",
                evidence: ["ambiguous extension"],
                uncertaintyNote: "Needs more context."));
        var confirmer = new NonImplementationSemanticConfirmer(store, runner, TestOptions);

        NonImplementationSemanticConfirmationBatchResult result =
            await confirmer.ConfirmAsync(
                new NonImplementationArtifactClassificationSet(
                    "slice-test",
                    [Candidate("notes/archive.custom", "hash-a", NonImplementationArtifactRoute.AmbiguousForSemanticReview)]),
                DateTimeOffset.UnixEpoch);

        NonImplementationReviewLedgerEntry entry = Assert.Single(result.ConfirmedEntries);
        Assert.Equal(NonImplementationSemanticDisposition.Uncertain, entry.SemanticDisposition);
        Assert.Equal("Needs more context.", entry.SemanticUncertaintyNote);
        Assert.Single(runner.Requests);
    }

    [Fact]
    public async Task Service_does_not_process_deterministic_exclusions()
    {
        var artifacts = new InMemoryArtifactStore();
        var store = new NonImplementationReviewLedgerStore(artifacts);
        var runner = new RecordingReviewRunner(_ => throw new InvalidOperationException("Should not run."));
        var confirmer = new NonImplementationSemanticConfirmer(store, runner, TestOptions);

        NonImplementationSemanticConfirmationBatchResult result =
            await confirmer.ConfirmAsync(
                new NonImplementationArtifactClassificationSet(
                    "slice-test",
                    [Candidate("src/App/Foo.cs", "hash-a", NonImplementationArtifactRoute.ExcludedImplementationArtifact)]),
                DateTimeOffset.UnixEpoch);

        Assert.Equal(1, result.IgnoredCount);
        Assert.Empty(runner.Requests);
        Assert.Empty((await store.LoadOrCreateAsync()).Entries);
    }

    [Fact]
    public void Service_rejects_mutation_capable_runner_adapter()
    {
        var store = new NonImplementationReviewLedgerStore(new InMemoryArtifactStore());
        var runner = new RecordingReviewRunner(_ => throw new InvalidOperationException())
        {
            Capabilities = new NonImplementationReviewRunnerConstraints(
                allowsWorkspaceWrites: true,
                allowsCommits: false,
                allowsPushes: false,
                allowsMutationCapableScopedOperations: false),
        };

        Assert.Throws<InvalidOperationException>(
            () => new NonImplementationSemanticConfirmer(store, runner, TestOptions));
    }

    [Fact]
    public async Task Agent_review_runner_uses_read_only_planning_spec_without_scoped_mutation_profile()
    {
        var runtime = new RecordingAgentRuntime("""{"ledgerEntryId":"ni-test","candidatePath":"docs/design.md","reviewedContentSha256":"hash-a","reviewedFileDeleted":false,"deletedReviewedIdentity":null,"disposition":"FalsePositive","rationale":"not prose","evidenceExcerptsOrPathFacts":["path fact"],"uncertaintyNote":null}""");
        var repository = new Repository
        {
            Id = Guid.NewGuid(),
            Name = "repo",
            Path = "C:/repo",
        };
        var runner = new AgentNonImplementationReviewRunner(runtime, repository);
        var request = new NonImplementationReviewRunnerRequest(
            "ConfirmNonImplementationCandidate",
            "prompt",
            maxPromptPayloadCharacters: 100);

        NonImplementationReviewRunnerResponse response =
            await runner.RunAsync(request, CancellationToken.None);

        AgentSessionSpec spec = Assert.Single(runtime.OneShotSpecs);
        Assert.Equal(SessionRole.Planning, spec.Role);
        Assert.Equal("read-only", spec.Sandbox.Identifier);
        Assert.False(spec.Sandbox.CanWriteWorkspace);
        Assert.False(spec.Sandbox.CanAccessNetwork);
        Assert.False(spec.Sandbox.RequiresApproval);
        Assert.Null(spec.OperationPermissionProfile);
        Assert.Empty(runtime.OpenedSpecs);
        Assert.Equal("prompt", Assert.Single(runtime.Prompts));
        Assert.Contains("\"FalsePositive\"", response.StructuredText, StringComparison.Ordinal);
    }

    private static NonImplementationReviewRunnerResponse ResponseFromRequest(
        NonImplementationReviewRunnerRequest request,
        NonImplementationSemanticDisposition disposition,
        string rationale = "semantic rationale",
        IReadOnlyList<string>? evidence = null,
        string? uncertaintyNote = null)
    {
        using JsonDocument document = JsonDocument.Parse(ExtractInputPayload(request.PromptPayload));
        JsonElement root = document.RootElement;
        bool reviewedFileDeleted = root.GetProperty("reviewedFileDeleted").GetBoolean();
        string? reviewedContentSha256 = root.GetProperty("reviewedContentSha256").ValueKind == JsonValueKind.Null
            ? null
            : root.GetProperty("reviewedContentSha256").GetString();
        string? deletedReviewedIdentity = root.GetProperty("deletedReviewedIdentity").ValueKind == JsonValueKind.Null
            ? null
            : root.GetProperty("deletedReviewedIdentity").GetString();

        string json = JsonSerializer.Serialize(new
        {
            ledgerEntryId = root.GetProperty("ledgerEntryId").GetString(),
            candidatePath = root.GetProperty("candidatePath").GetString(),
            reviewedContentSha256,
            reviewedFileDeleted,
            deletedReviewedIdentity,
            disposition,
            rationale,
            evidenceExcerptsOrPathFacts = evidence ?? ["path fact"],
            uncertaintyNote = disposition == NonImplementationSemanticDisposition.Uncertain
                ? uncertaintyNote ?? "Needs more context."
                : null,
        }, JsonOptions);
        return new NonImplementationReviewRunnerResponse(json);
    }

    private static string ExtractInputPayload(string prompt)
    {
        const string marker = "```json";
        int start = prompt.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException("Prompt did not include an input JSON block.");
        }

        int contentStart = prompt.IndexOf('\n', start);
        if (contentStart < 0)
        {
            throw new InvalidOperationException("Prompt input JSON block was malformed.");
        }

        int end = prompt.IndexOf("```", contentStart + 1, StringComparison.Ordinal);
        if (end < 0)
        {
            throw new InvalidOperationException("Prompt input JSON block was unterminated.");
        }

        return prompt[(contentStart + 1)..end].Trim();
    }

    private static string ConfirmationJson(
        NonImplementationReviewLedgerEntry entry,
        NonImplementationSemanticDisposition disposition = NonImplementationSemanticDisposition.ConfirmedNonImplementation,
        string? reviewedContentSha256 = null,
        bool? reviewedFileDeleted = null,
        string? deletedReviewedIdentity = null)
    {
        bool deleted = reviewedFileDeleted ?? entry.ReviewedFileDeleted;
        string json = JsonSerializer.Serialize(new
        {
            ledgerEntryId = entry.EntryId,
            candidatePath = entry.Path,
            reviewedContentSha256 = deleted ? null : reviewedContentSha256 ?? entry.ReviewedContentSha256,
            reviewedFileDeleted = deleted,
            deletedReviewedIdentity = deleted
                ? deletedReviewedIdentity ?? NonImplementationReviewLedgerStore.DeletedReviewedIdentity(entry)
                : null,
            disposition,
            rationale = "semantic rationale",
            evidenceExcerptsOrPathFacts = new[] { "path fact" },
            uncertaintyNote = disposition == NonImplementationSemanticDisposition.Uncertain
                ? "Needs more context."
                : null,
        }, JsonOptions);
        return json;
    }

    private static NonImplementationReviewLedgerEntry Entry(
        string path = "docs/design.md",
        string? hash = "hash-a",
        bool deleted = false,
        string? baselineHash = null) =>
        new()
        {
            EntryId = "ni-test",
            ExecutionSliceId = "slice-test",
            Path = path,
            ReviewedContentSha256 = deleted ? null : hash,
            ReviewedFileDeleted = deleted,
            BaselineContentSha256 = baselineHash,
            Route = NonImplementationArtifactRoute.SemanticReviewCandidate,
            ClassificationRuleId = "rule",
            ClassificationRationale = "candidate",
            ClassificationPathFacts = [$"path={path}"],
            ClassifierVersion = NonImplementationArtifactClassifier.Version,
            ConfirmationPromptSourceHash = TestOptions.ConfirmationPromptSourceHash,
            FirstSeenAtUtc = DateTimeOffset.UnixEpoch,
            LastSeenAtUtc = DateTimeOffset.UnixEpoch,
            HitlProvenanceKind = NonImplementationHitlProvenanceKind.None,
            ResolutionState = NonImplementationResolutionState.Unresolved,
        };

    private static NonImplementationArtifactClassification Candidate(
        string path,
        string? hash,
        NonImplementationArtifactRoute route = NonImplementationArtifactRoute.SemanticReviewCandidate,
        bool deleted = false,
        string? baselineHash = null)
    {
        var file = new RepositoryChangedFileFacts(
            "slice-test",
            path,
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
            route,
            route == NonImplementationArtifactRoute.AmbiguousForSemanticReview
                ? "ambiguous-unknown-file"
                : "likely-prose-design-audit-roadmap-report",
            [$"path={path}", $"postSha256={hash ?? "<none>"}"],
            "The changed file needs semantic review.",
            NonImplementationArtifactClassifier.Version);
    }

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

    private sealed class RecordingAgentRuntime(string output) : IAgentRuntime
    {
        public List<AgentSessionSpec> OneShotSpecs { get; } = [];

        public List<AgentSessionSpec> OpenedSpecs { get; } = [];

        public List<string> Prompts { get; } = [];

        public Task<IAgentSession> OpenSessionAsync(
            AgentSessionSpec spec,
            CancellationToken cancellationToken = default)
        {
            OpenedSpecs.Add(spec);
            throw new NotSupportedException("The non-implementation review runner must use one-shot read-only review.");
        }

        public Task<AgentTurnResult> RunOneShotAsync(
            AgentSessionSpec spec,
            string prompt,
            Func<AgentStreamChunk, Task>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            OneShotSpecs.Add(spec);
            Prompts.Add(prompt);
            return Task.FromResult(new AgentTurnResult(
                TurnIndex: 1,
                AgentTurnState.Completed,
                output,
                new AgentTokenUsage(PromptTokens: 1, OutputTokens: 1)));
        }

        public ValueTask CloseSessionAsync(IAgentSession session) => ValueTask.CompletedTask;
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
