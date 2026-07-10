namespace LoopRelay.Orchestration.Recovery;

public sealed class RecoveryJournal
{
    private static readonly IReadOnlyDictionary<RecoveryAttemptStatus, RecoveryAttemptStatus[]> Allowed =
        new Dictionary<RecoveryAttemptStatus, RecoveryAttemptStatus[]>
        {
            [RecoveryAttemptStatus.Pending] =
            [
                RecoveryAttemptStatus.ProtocolRepairRequired,
                RecoveryAttemptStatus.ResumeSucceeded,
                RecoveryAttemptStatus.RecoveryPreparing,
                RecoveryAttemptStatus.RecoveryFailed,
                RecoveryAttemptStatus.UnknownOutcome,
            ],
            [RecoveryAttemptStatus.RecoveryPreparing] =
            [RecoveryAttemptStatus.ReplacementCreating, RecoveryAttemptStatus.RecoveryFailed],
            [RecoveryAttemptStatus.ReplacementCreating] =
            [RecoveryAttemptStatus.ReplacementCreated, RecoveryAttemptStatus.RecoveryFailed, RecoveryAttemptStatus.UnknownOutcome],
            [RecoveryAttemptStatus.ReplacementCreated] =
            [RecoveryAttemptStatus.ContextInjectionPending, RecoveryAttemptStatus.RecoveryCompleted, RecoveryAttemptStatus.RecoveryFailed],
            [RecoveryAttemptStatus.ContextInjectionPending] =
            [RecoveryAttemptStatus.RecoveryCompleted, RecoveryAttemptStatus.RecoveryFailed, RecoveryAttemptStatus.UnknownOutcome],
            [RecoveryAttemptStatus.UnknownOutcome] =
            [RecoveryAttemptStatus.ReplacementCreated, RecoveryAttemptStatus.ContextInjectionPending, RecoveryAttemptStatus.RecoveryCompleted, RecoveryAttemptStatus.RecoveryFailed],
        };

    public RecoveryAttempt Begin(
        string attemptId,
        string? previousAttemptId,
        string scopeId,
        string originalLineageId,
        string? transitionRunId,
        string profileDigest,
        string trigger,
        string idempotencyKey,
        DateTimeOffset now) =>
        new(
            Required(attemptId), previousAttemptId, Required(scopeId), Required(originalLineageId), null,
            transitionRunId, RecoveryAttemptStatus.Pending, 0, Required(profileDigest), null, null,
            Required(trigger), null, Required(idempotencyKey), null, null, 0, now, now, null);

    public RecoveryJournalTransition RecordResumeFailure(
        RecoveryAttempt attempt,
        RecoveryFailure failure,
        bool replacementEligible,
        DateTimeOffset now)
    {
        if (failure.TurnSubmitted && replacementEligible)
        {
            throw new InvalidOperationException("A post-turn failure cannot be eligible for automatic replacement.");
        }

        RecoveryAttemptStatus next = failure.Classification switch
        {
            "DeterministicProtocolFailure" => RecoveryAttemptStatus.ProtocolRepairRequired,
            "UnavailableSession" or "CorruptedState" when replacementEligible => RecoveryAttemptStatus.RecoveryPreparing,
            "UnknownOutcome" => RecoveryAttemptStatus.UnknownOutcome,
            _ => RecoveryAttemptStatus.RecoveryFailed,
        };
        return Transition(attempt, next, now, failure: failure, completed: IsTerminal(next));
    }

    public RecoveryJournalTransition RecordResumeSuccess(RecoveryAttempt attempt, DateTimeOffset now) =>
        Transition(attempt, RecoveryAttemptStatus.ResumeSucceeded, now, completed: true);

    public RecoveryJournalTransition RecordPlan(RecoveryAttempt attempt, RecoveryPlan plan, DateTimeOffset now)
    {
        if (attempt.Status != RecoveryAttemptStatus.RecoveryPreparing)
        {
            throw new InvalidOperationException("A recovery plan may only be attached in RecoveryPreparing.");
        }

        RecoveryAttempt updated = attempt with
        {
            PlanDigest = plan.Digest,
            Mechanism = plan.Mechanism,
            RowVersion = attempt.RowVersion + 1,
            UpdatedAt = now,
        };
        return new RecoveryJournalTransition(updated,
            [new RecoveryDomainEvent(attempt.AttemptId, attempt.Status, attempt.Status, "RecoveryPlanRecorded", now)]);
    }

    public RecoveryJournalTransition BeginReplacement(RecoveryAttempt attempt, DateTimeOffset now) =>
        Transition(attempt, RecoveryAttemptStatus.ReplacementCreating, now);

    public RecoveryJournalTransition RecordReplacementCreated(
        RecoveryAttempt attempt,
        string replacementLineageId,
        string? providerRequestId,
        string? providerCorrelationId,
        DateTimeOffset now) =>
        Transition(attempt, RecoveryAttemptStatus.ReplacementCreated, now,
            replacementLineageId: Required(replacementLineageId),
            providerRequestId: providerRequestId,
            providerCorrelationId: providerCorrelationId);

    public RecoveryJournalTransition RecordContextPending(RecoveryAttempt attempt, DateTimeOffset now) =>
        Transition(attempt, RecoveryAttemptStatus.ContextInjectionPending, now);

    public RecoveryJournalTransition Complete(RecoveryAttempt attempt, DateTimeOffset now) =>
        Transition(attempt, RecoveryAttemptStatus.RecoveryCompleted, now, completed: true);

    public RecoveryJournalTransition Fail(RecoveryAttempt attempt, RecoveryFailure failure, DateTimeOffset now) =>
        Transition(attempt, RecoveryAttemptStatus.RecoveryFailed, now, failure: failure, completed: true);

    public RecoveryJournalTransition RecordUnknownOutcome(RecoveryAttempt attempt, RecoveryFailure failure, DateTimeOffset now) =>
        Transition(attempt, RecoveryAttemptStatus.UnknownOutcome, now, failure: failure);

    public RecoveryJournalTransition IncrementSideEffectFreeRetry(RecoveryAttempt attempt, DateTimeOffset now)
    {
        EnsureMutable(attempt);
        if (attempt.Status != RecoveryAttemptStatus.Pending)
        {
            throw new InvalidOperationException("Only a Pending side-effect-free operation may be retried.");
        }

        RecoveryAttempt updated = attempt with
        {
            RetryCount = attempt.RetryCount + 1,
            RowVersion = attempt.RowVersion + 1,
            UpdatedAt = now,
        };
        return new RecoveryJournalTransition(updated,
            [new RecoveryDomainEvent(attempt.AttemptId, attempt.Status, attempt.Status, "SideEffectFreeRetryRecorded", now)]);
    }

    private static RecoveryJournalTransition Transition(
        RecoveryAttempt attempt,
        RecoveryAttemptStatus next,
        DateTimeOffset now,
        RecoveryFailure? failure = null,
        bool completed = false,
        string? replacementLineageId = null,
        string? providerRequestId = null,
        string? providerCorrelationId = null)
    {
        EnsureMutable(attempt);
        if (!Allowed.TryGetValue(attempt.Status, out RecoveryAttemptStatus[]? successors)
            || !successors.Contains(next))
        {
            throw new InvalidOperationException($"Recovery transition {attempt.Status} -> {next} is not allowed.");
        }

        RecoveryAttempt updated = attempt with
        {
            Status = next,
            Failure = failure ?? attempt.Failure,
            ReplacementLineageId = replacementLineageId ?? attempt.ReplacementLineageId,
            ProviderRequestId = providerRequestId ?? attempt.ProviderRequestId,
            ProviderCorrelationId = providerCorrelationId ?? attempt.ProviderCorrelationId,
            RowVersion = attempt.RowVersion + 1,
            UpdatedAt = now,
            CompletedAt = completed ? now : null,
        };
        return new RecoveryJournalTransition(updated,
            [new RecoveryDomainEvent(attempt.AttemptId, attempt.Status, next, EventName(next), now)]);
    }

    private static bool IsTerminal(RecoveryAttemptStatus status) => status is
        RecoveryAttemptStatus.ProtocolRepairRequired or RecoveryAttemptStatus.ResumeSucceeded
        or RecoveryAttemptStatus.RecoveryCompleted or RecoveryAttemptStatus.RecoveryFailed;

    private static void EnsureMutable(RecoveryAttempt attempt)
    {
        if (IsTerminal(attempt.Status))
        {
            throw new InvalidOperationException($"Terminal recovery attempt {attempt.AttemptId} is immutable.");
        }
    }

    private static string EventName(RecoveryAttemptStatus status) => status switch
    {
        RecoveryAttemptStatus.ProtocolRepairRequired => "ProtocolRepairRequired",
        RecoveryAttemptStatus.ResumeSucceeded => "ResumeSucceeded",
        RecoveryAttemptStatus.RecoveryPreparing => "RecoveryPreparing",
        RecoveryAttemptStatus.ReplacementCreating => "ReplacementCreating",
        RecoveryAttemptStatus.ReplacementCreated => "ReplacementCreated",
        RecoveryAttemptStatus.ContextInjectionPending => "ContextInjectionPending",
        RecoveryAttemptStatus.RecoveryCompleted => "RecoveryCompleted",
        RecoveryAttemptStatus.RecoveryFailed => "RecoveryFailed",
        RecoveryAttemptStatus.UnknownOutcome => "UnknownOutcome",
        _ => status.ToString(),
    };

    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value;
}
