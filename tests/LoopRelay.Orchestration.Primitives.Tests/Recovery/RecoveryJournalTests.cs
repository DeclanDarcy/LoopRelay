using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Orchestration.Tests.Recovery;

public sealed class RecoveryJournalTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    public void DeterministicProtocolFailureBecomesTerminalProtocolRepairRequired()
    {
        RecoveryJournal journal = new();
        RecoveryAttempt pending = Begin(journal);

        RecoveryJournalTransition transition = journal.RecordResumeFailure(
            pending,
            Failure("DeterministicProtocolFailure"),
            replacementEligible: false,
            Now.AddSeconds(1));

        Assert.Equal(RecoveryAttemptStatus.ProtocolRepairRequired, transition.Attempt.Status);
        Assert.Equal(Now.AddSeconds(1), transition.Attempt.CompletedAt);
        Assert.Equal(pending.RowVersion + 1, transition.Attempt.RowVersion);
        Assert.Throws<InvalidOperationException>(() => journal.Fail(
            transition.Attempt, Failure("ProgrammingFailure"), Now.AddSeconds(2)));
    }

    [Theory]
    [InlineData("UnavailableSession")]
    [InlineData("CorruptedState")]
    public void EligiblePreTurnFailureEntersRecoveryPreparing(string classification)
    {
        RecoveryJournal journal = new();
        RecoveryJournalTransition transition = journal.RecordResumeFailure(
            Begin(journal), Failure(classification), replacementEligible: true, Now.AddSeconds(1));

        Assert.Equal(RecoveryAttemptStatus.RecoveryPreparing, transition.Attempt.Status);
        Assert.Null(transition.Attempt.CompletedAt);
    }

    [Fact]
    public void TurnSubmittedCanNeverBeReplacementEligible()
    {
        RecoveryJournal journal = new();
        RecoveryFailure postTurn = Failure("UnavailableSession") with { TurnSubmitted = true };

        Assert.Throws<InvalidOperationException>(() =>
            journal.RecordResumeFailure(Begin(journal), postTurn, replacementEligible: true, Now));
    }

    [Fact]
    public void ReplacementLifecycleIsMonotonicAndCarriesOnePersistedPlan()
    {
        RecoveryJournal journal = new();
        RecoveryAttempt attempt = journal.RecordResumeFailure(
            Begin(journal), Failure("UnavailableSession"), true, Now.AddSeconds(1)).Attempt;
        RecoveryPlan plan = RecoveryPlanTests.Plan();
        attempt = journal.RecordPlan(attempt, plan, Now.AddSeconds(2)).Attempt;
        attempt = journal.BeginReplacement(attempt, Now.AddSeconds(3)).Attempt;
        attempt = journal.RecordReplacementCreated(
            attempt, "lineage-child", "request-1", "correlation-1", Now.AddSeconds(4)).Attempt;
        attempt = journal.RecordContextPending(attempt, Now.AddSeconds(5)).Attempt;
        attempt = journal.Complete(attempt, Now.AddSeconds(6)).Attempt;

        Assert.Equal(RecoveryAttemptStatus.RecoveryCompleted, attempt.Status);
        Assert.Equal(plan.Digest, attempt.PlanDigest);
        Assert.Equal(plan.Mechanism, attempt.Mechanism);
        Assert.Equal("lineage-child", attempt.ReplacementLineageId);
        Assert.Equal(6, attempt.RowVersion);
    }

    [Fact]
    public void ForbiddenTransitionAndRetryAfterSideEffectAreRejected()
    {
        RecoveryJournal journal = new();
        RecoveryAttempt preparing = journal.RecordResumeFailure(
            Begin(journal), Failure("UnavailableSession"), true, Now).Attempt;

        Assert.Throws<InvalidOperationException>(() => journal.Complete(preparing, Now));
        Assert.Throws<InvalidOperationException>(() => journal.IncrementSideEffectFreeRetry(preparing, Now));
    }

    private static RecoveryAttempt Begin(RecoveryJournal journal) =>
        journal.Begin("attempt-1", null, "scope-1", "lineage-original", "run-1", "profile-digest", "ResumeFailure", "idem-1", Now);

    private static RecoveryFailure Failure(string classification) =>
        new(classification, "thread/resume", -32602, "profile-digest", "redacted", TurnSubmitted: false);
}
