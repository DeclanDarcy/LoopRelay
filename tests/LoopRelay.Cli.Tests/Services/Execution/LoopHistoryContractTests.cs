using LoopRelay.Cli.Abstractions.Persistence;
using LoopRelay.Core.Models.Identity;
using Xunit;

namespace LoopRelay.Cli.Tests.Services.Execution;

public sealed class LoopHistoryContractTests
{
    [Fact]
    public void History_fact_has_stable_identity_and_validated_content_hash()
    {
        CanonicalCausalContext causality = NewAttempt();
        const string content = "decision evidence";

        var fact = new LoopHistoryRecord(
            HistoryFactIdentity.New(),
            LoopHistoryKind.Decisions,
            1,
            DateTimeOffset.UtcNow,
            content,
            LoopHistoryRecord.ComputeContentHash(content),
            causality,
            HistoryEvidenceAttachments.Empty);

        Assert.False(fact.Identity.IsEmpty);
        Assert.Equal(causality, fact.Causality);
        Assert.Null(fact.MaterializedRelativePath);
    }

    [Fact]
    public void Provider_and_recovery_identifiers_are_evidence_not_canonical_identity()
    {
        var attachments = new HistoryEvidenceAttachments(
            Provider: new HistoryProviderEvidence("codex", "provider-thread", "provider-turn"),
            Continuity: new HistoryContinuityEvidence(
                new ContinuityLineageIdentity("lineage_1"),
                "resume"),
            Recovery: new HistoryRecoveryEvidence(
                new RecoveryAttemptIdentity("recovery_1")));

        var request = new LoopHistoryAppendRequest(
            LoopHistoryKind.Handoff,
            "handoff",
            NewAttempt(),
            attachments);

        Assert.Equal("provider-thread", request.Evidence.Provider!.ProviderThreadId);
        Assert.False(request.Causality.Attempt.IsEmpty);
    }

    [Fact]
    public void History_fact_rejects_content_hash_mismatch()
    {
        Assert.Throws<ArgumentException>(() => new LoopHistoryRecord(
            HistoryFactIdentity.New(),
            LoopHistoryKind.OperationalDelta,
            1,
            DateTimeOffset.UtcNow,
            "delta",
            "wrong",
            NewAttempt(),
            HistoryEvidenceAttachments.Empty));
    }

    private static CanonicalCausalContext NewAttempt() =>
        new(
            WorkspaceIdentity.New(),
            RunIdentity.New(),
            WorkflowInstanceIdentity.New(),
            TransitionRunIdentity.New(),
            AttemptIdentity.New());
}
