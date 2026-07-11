using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Cli.Services.Cli;
using LoopRelay.Cli.Services.Execution;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Services.Artifacts;
using LoopRelay.Orchestration.Persistence;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Workflows;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Cli;

public sealed class LedgerEvidenceRetrievalTests
{
    [Fact]
    public async Task Receipt_consumed_raw_output_is_retrievable_from_the_ledger_by_its_hash()
    {
        Repository repository = TempRepo();
        // The adversarial-review body reaches its consumer via a gitignored .LoopRelay file, but
        // the transition that produced it appended the raw output to the evidence ledger — the
        // receipt hash of the consumed file must resolve to that exact content.
        string reviewBody = "# Adversarial Review\n\nFinding: the plan misses rollback.";
        var evidenceStore = new CanonicalTransitionEvidenceStore(new CanonicalWorkflowPersistenceStore(repository));
        await evidenceStore.RecordRawOutputAsync(
            "tr_review",
            new WorkflowTransitionIdentity("RunAdversarialReview"),
            new PromptExecutionResult(
                PromptExecutionStatus.Completed,
                reviewBody,
                TimeSpan.FromSeconds(3),
                new Dictionary<string, string>()),
            CancellationToken.None);

        LedgerEvidenceMatch? match = await LedgerEvidenceRetrieval.TryResolveContentByHashAsync(
            repository,
            ConsumedInputFile.HashContent(reviewBody),
            CancellationToken.None);

        Assert.NotNull(match);
        Assert.Equal(reviewBody, match.Content);
        Assert.Equal("canonical_transition_evidence", match.Source);
    }

    [Fact]
    public async Task Rotated_loop_history_is_retrievable_by_its_content_hash()
    {
        Repository repository = TempRepo();
        var store = new LedgerLoopHistoryStore(new FileSystemArtifactStore(), repository);
        await store.AppendAsync(LoopHistoryKind.Handoff, "handoff body", new LoopHistoryLineage(null, "tr_handoff", null));

        LedgerEvidenceMatch? match = await LedgerEvidenceRetrieval.TryResolveContentByHashAsync(
            repository,
            ConsumedInputFile.HashContent("handoff body"),
            CancellationToken.None);

        Assert.NotNull(match);
        Assert.Equal("handoff body", match.Content);
        Assert.Equal("loop_history", match.Source);
        Assert.StartsWith("hist_", match.Identity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_hash_resolves_to_null()
    {
        Repository repository = TempRepo();
        var store = new LedgerLoopHistoryStore(new FileSystemArtifactStore(), repository);
        await store.AppendAsync(LoopHistoryKind.Decisions, "known content");

        Assert.Null(await LedgerEvidenceRetrieval.TryResolveContentByHashAsync(
            repository,
            ConsumedInputFile.HashContent("content that was never recorded"),
            CancellationToken.None));
    }

    [Fact]
    public async Task Missing_workspace_database_resolves_to_null()
    {
        Repository repository = TempRepo();

        Assert.Null(await LedgerEvidenceRetrieval.TryResolveContentByHashAsync(
            repository,
            ConsumedInputFile.HashContent("anything"),
            CancellationToken.None));
    }

    private static Repository TempRepo()
    {
        string root = Directory.CreateTempSubdirectory("cc-cli-ledger-retrieval").FullName;
        return new Repository
        {
            Id = Guid.NewGuid(),
            Name = Path.GetFileName(root),
            Path = root,
        };
    }
}
