using System.Security.Cryptography;
using System.Text;
using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;

namespace LoopRelay.Orchestration.Recovery;

public sealed class RecoveryRuntime(
    IRecoveryStore _store,
    IAgentSessionContinuityRuntime _continuity,
    IRecoverySourceCatalog _sources,
    IRecoveryPlanner _planner,
    IRecoveryMechanismCatalog _mechanisms,
    IRecoveryEnvelopeFactory _envelopes,
    TimeProvider? _timeProvider = null) : IRecoveryRuntime
{
    private readonly RecoveryJournal _journal = new();
    private readonly TimeProvider _clock = _timeProvider ?? TimeProvider.System;

    public async Task<RecoveryRuntimeResult> RunAsync(
        RecoveryRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ActiveStateReadResult activeRead = await _store.ReadActiveAsync(request.ScopeId, cancellationToken);
        if (activeRead.Status != ActiveStateReadStatus.Present
            || activeRead.Active is null
            || activeRead.Lineage is null)
        {
            return new RecoveryRuntimeResult(
                RecoveryRuntimeOutcome.NoActiveSession, null, null, null, null,
                RecoveryCompleteness.Unknown, false,
                activeRead.Diagnostic ?? $"Active state is {activeRead.Status}.");
        }

        DecisionSessionActiveState active = activeRead.Active;
        DecisionSessionLineageNode originalLineage = activeRead.Lineage;
        var original = new ProviderSessionReference(originalLineage.Provider, originalLineage.ProviderSessionId);
        RecoveryAttempt? attempt = await _store.ReadNonterminalAttemptAsync(request.ScopeId, cancellationToken);
        if (attempt is null)
        {
            DateTimeOffset now = Now();
            string attemptId = $"attempt-{Hash($"{request.ScopeId}|{request.TransitionRunId}|{originalLineage.LineageId}")[..24]}";
            attempt = _journal.Begin(
                attemptId,
                null,
                request.ScopeId,
                originalLineage.LineageId,
                request.TransitionRunId,
                request.Profile.Digest,
                request.Trigger,
                $"resume-{Hash(attemptId)}",
                now);
            RecoveryStoreWriteResult begun = await _store.BeginAttemptAsync(
                attempt, active.RowVersion, request.Profile, cancellationToken);
            if (!begun.Succeeded)
            {
                return Failed(attempt, null, begun.Diagnostic ?? "The recovery attempt could not be persisted.");
            }
        }

        if (attempt.ProfileDigest != request.Profile.Digest
            || attempt.OriginalLineageId != originalLineage.LineageId)
        {
            return Failed(attempt, null, "The nonterminal attempt does not match the active lineage or captured profile.");
        }

        if (attempt.Status == RecoveryAttemptStatus.Pending)
        {
            RecoveryRuntimeResult? resumed = await TryResumeAsync(
                request, original, active, attempt, cancellationToken);
            if (resumed is not null)
            {
                return resumed;
            }

            attempt = await RequiredAttemptAsync(attempt.AttemptId, cancellationToken);
        }

        if (attempt.Status == RecoveryAttemptStatus.UnknownOutcome)
        {
            return new RecoveryRuntimeResult(
                RecoveryRuntimeOutcome.UnknownOutcome, null, original, attempt, null,
                RecoveryCompleteness.Unknown, false,
                "A provider side effect is uncertain; only explicit reconciliation may continue this attempt.");
        }

        if (attempt.Status is RecoveryAttemptStatus.ProtocolRepairRequired or RecoveryAttemptStatus.RecoveryFailed)
        {
            return Failed(attempt, null, attempt.Failure?.RedactedDiagnostic ?? attempt.Status.ToString());
        }

        if (attempt.Status == RecoveryAttemptStatus.ResumeSucceeded)
        {
            return new RecoveryRuntimeResult(
                RecoveryRuntimeOutcome.ResumedOriginal, null, original, attempt, null,
                RecoveryCompleteness.Full, false, null);
        }

        return await ContinueRecoveryAsync(
            request, original, originalLineage, active, attempt, cancellationToken);
    }

    private async Task<RecoveryRuntimeResult?> TryResumeAsync(
        RecoveryRuntimeRequest request,
        ProviderSessionReference original,
        DecisionSessionActiveState active,
        RecoveryAttempt attempt,
        CancellationToken cancellationToken)
    {
        SessionResumeResult resume = await _continuity.ResumeSessionAsync(
            new SessionResumeRequest(request.ResumeSessionSpec, original, request.Profile, attempt.RetryCount + 1),
            cancellationToken);
        if (resume.Outcome == SessionResumeOutcome.SuccessfulResume
            && resume.Session is not null
            && resume.Resolved == original)
        {
            RecoveryAttempt succeeded = _journal.RecordResumeSuccess(attempt, Now()).Attempt;
            RecoveryStoreWriteResult written = await _store.CompareAndSwapAttemptAsync(attempt, succeeded, cancellationToken);
            if (!written.Succeeded)
            {
                await _continuity.CloseSessionAsync(resume.Session);
                return Failed(attempt, null, written.Diagnostic ?? "Resume succeeded but could not be journaled.");
            }

            return new RecoveryRuntimeResult(
                RecoveryRuntimeOutcome.ResumedOriginal,
                resume.Session,
                resume.Resolved,
                succeeded,
                null,
                RecoveryCompleteness.Full,
                false,
                null);
        }

        int retryCeiling = request.Policy.TryGetValue("resume-retry-ceiling", out string? configuredRetry)
            && int.TryParse(configuredRetry, out int parsedRetry) && parsedRetry >= 0
                ? parsedRetry
                : 2;
        if (resume.Outcome == SessionResumeOutcome.RetryableFailure
            && !resume.Transport.TurnSubmitted
            && attempt.RetryCount < retryCeiling)
        {
            RecoveryAttempt retried = _journal.IncrementSideEffectFreeRetry(attempt, Now()).Attempt;
            RecoveryStoreWriteResult retryWrite = await _store.CompareAndSwapAttemptAsync(
                attempt, retried, cancellationToken);
            if (!retryWrite.Succeeded)
            {
                return Failed(attempt, null, retryWrite.Diagnostic ?? "The transient retry could not be persisted.");
            }

            int delayMilliseconds = (int)(Convert.ToUInt32(Hash(attempt.AttemptId)[..8], 16) % 17);
            if (delayMilliseconds > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), _clock, cancellationToken);
            }
            return await TryResumeAsync(request, original, active, retried, cancellationToken);
        }

        RecoveryFailure failure = Failure(resume);
        RecoveryAttempt classified = _journal.RecordResumeFailure(
            attempt, failure, resume.IsReplacementEligible, Now()).Attempt;
        RecoveryStoreWriteResult stored = await _store.CompareAndSwapAttemptAsync(attempt, classified, cancellationToken);
        if (!stored.Succeeded)
        {
            return Failed(attempt, null, stored.Diagnostic ?? "Resume failure could not be journaled.");
        }

        return classified.Status switch
        {
            RecoveryAttemptStatus.RecoveryPreparing => null,
            RecoveryAttemptStatus.ProtocolRepairRequired => new RecoveryRuntimeResult(
                RecoveryRuntimeOutcome.ProtocolRepairRequired, null, original, classified, null,
                RecoveryCompleteness.Unknown, false, failure.RedactedDiagnostic),
            RecoveryAttemptStatus.UnknownOutcome => new RecoveryRuntimeResult(
                RecoveryRuntimeOutcome.UnknownOutcome, null, original, classified, null,
                RecoveryCompleteness.Unknown, false, failure.RedactedDiagnostic),
            _ => Failed(classified, null, failure.RedactedDiagnostic),
        };
    }

    private async Task<RecoveryRuntimeResult> ContinueRecoveryAsync(
        RecoveryRuntimeRequest request,
        ProviderSessionReference original,
        DecisionSessionLineageNode originalLineage,
        DecisionSessionActiveState active,
        RecoveryAttempt attempt,
        CancellationToken cancellationToken)
    {
        if (attempt.Failure is null)
        {
            return Failed(attempt, null, "RecoveryPreparing has no structured resume failure.");
        }

        IReadOnlyList<RecoverySourceObservation> observations;
        try
        {
            var collected = new List<RecoverySourceObservation>();
            foreach (IRecoverySource source in _sources.All)
            {
                RecoverySourceObservation? observation = await source.ObserveAsync(
                    new RecoverySourceRequest(
                        request.ScopeId, request.ResumeSessionSpec, original, request.Profile),
                    cancellationToken);
                if (observation is not null)
                {
                    collected.Add(observation);
                }
            }

            observations = collected
                .OrderBy(item => item.Descriptor.Order)
                .ThenBy(item => item.Descriptor.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Descriptor.Digest, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return await FailAttemptAsync(attempt, null, "RecoverySourceFailure", exception.GetType().Name, cancellationToken);
        }

        RecoveryEnvelopePayload? envelope = null;
        string? envelopeFailure = null;
        try
        {
            envelope = _envelopes.Build(
                attempt.AttemptId, request.ScopeId, original, attempt.Failure,
                observations, request.Profile, request.ContextBudget);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            envelopeFailure = exception.Message;
        }

        RecoveryPlan? plan = attempt.PlanDigest is null
            ? null
            : await _store.ReadPlanAsync(attempt.PlanDigest, cancellationToken);
        if (plan is null)
        {
            try
            {
                plan = _planner.Plan(
                    new RecoveryPlanningInput(
                        attempt.Failure,
                        request.ScopeId,
                        request.Profile,
                        observations.Select(item => item.Descriptor).ToArray(),
                        request.Policy,
                        request.ContextBudget,
                        envelope?.Digest,
                        envelope?.Descriptor),
                    _mechanisms.All);
            }
            catch (RecoveryPlanningException exception)
            {
                return await FailAttemptAsync(
                    attempt, null, "NoEligibleRecoveryMechanism",
                    string.Join(";", exception.Evidence.Append(envelopeFailure ?? string.Empty)), cancellationToken);
            }

            RecoveryAttempt planned = _journal.RecordPlan(attempt, plan, Now()).Attempt;
            RecoveryStoreWriteResult planWrite = await _store.RecordPlanAsync(
                attempt, planned, plan, cancellationToken);
            if (!planWrite.Succeeded)
            {
                return Failed(attempt, plan, planWrite.Diagnostic ?? "The selected recovery plan was not persisted.");
            }

            attempt = planned;
        }
        else if (plan.ContinuityProfileDigest != request.Profile.Digest
                 || plan.EnvelopeDigest != envelope?.Digest
                 || !SameSources(plan.Sources, observations))
        {
            return Failed(attempt, plan, "The persisted plan no longer matches the profile, envelope, or source digests.");
        }

        IRecoveryMechanism mechanism;
        try
        {
            mechanism = _mechanisms.Resolve(plan.Mechanism);
        }
        catch (InvalidOperationException exception)
        {
            return Failed(attempt, plan, exception.Message);
        }

        IAgentSession? replacementSession = null;
        ProviderSessionReference? replacementReference = null;
        DecisionSessionLineageNode? replacementLineage = null;
        RecoveryMechanismExecutionResult? creationResult = null;
        try
        {
            bool beganCreatingThisRun = false;
            if (attempt.Status == RecoveryAttemptStatus.RecoveryPreparing)
            {
                RecoveryAttempt creating = _journal.BeginReplacement(attempt, Now()).Attempt;
                RecoveryStoreWriteResult write = await _store.CompareAndSwapAttemptAsync(attempt, creating, cancellationToken);
                if (!write.Succeeded)
                {
                    return Failed(attempt, plan, write.Diagnostic ?? "ReplacementCreating was not persisted.");
                }
                attempt = creating;
                beganCreatingThisRun = true;
            }

            if (attempt.Status == RecoveryAttemptStatus.ReplacementCreating)
            {
                RecoveryMechanismExecutionRequest createRequest = ExecutionRequest(
                    RecoveryMechanismExecutionPhase.CreateReplacement, request, plan, original,
                    observations, envelope, null, null);
                RecoveryMechanismExecutionResult created = beganCreatingThisRun
                    ? await mechanism.ExecuteAsync(createRequest, cancellationToken)
                    : await mechanism.ReconcileAsync(
                        createRequest,
                        new RecoveryMechanismExecutionResult(
                            RecoveryMechanismExecutionStatus.UnknownOutcome,
                            null, null, null, null, null,
                            "Restarted after ReplacementCreating; reconciling without issuing a second side effect."),
                        cancellationToken);
                if (created.Status == RecoveryMechanismExecutionStatus.Succeeded
                    && created.Session is null
                    && created.Replacement is not null)
                {
                    AgentSessionSpec reconciledSpec = WithResume(
                        request.FreshSessionSpec, created.Replacement.ThreadId);
                    SessionResumeResult reopened = await _continuity.ResumeSessionAsync(
                        new SessionResumeRequest(
                            reconciledSpec, created.Replacement, request.Profile), cancellationToken);
                    if (reopened.Outcome == SessionResumeOutcome.SuccessfulResume && reopened.Session is not null)
                    {
                        created = created with { Session = reopened.Session };
                    }
                    else
                    {
                        created = created with
                        {
                            Status = RecoveryMechanismExecutionStatus.UnknownOutcome,
                            Diagnostic = "The uniquely reconciled replacement could not be reopened for validation.",
                        };
                    }
                }
                creationResult = created;
                if (created.Status != RecoveryMechanismExecutionStatus.Succeeded
                    || created.Session is null || created.Replacement is null)
                {
                    return await RecordMechanismFailureAsync(attempt, plan, created, cancellationToken);
                }

                replacementSession = created.Session;
                replacementReference = created.Replacement;
                string lineageId = $"lineage-{Hash($"{attempt.AttemptId}|{created.Replacement.ThreadId}")[..24]}";
                replacementLineage = new DecisionSessionLineageNode(
                    lineageId,
                    request.ScopeId,
                    created.Replacement.Provider,
                    created.Replacement.ThreadId,
                    originalLineage.LineageId,
                    originalLineage.RootLineageId,
                    mechanism.LineageMechanism,
                    plan.ExpectedCompleteness,
                    Hash(string.Join('|', plan.Sources.Select(source => source.Digest))),
                    request.Profile.Digest,
                    plan.Digest,
                    Now(),
                    null,
                    null,
                    "Inactive");
                RecoveryAttempt recorded = _journal.RecordReplacementCreated(
                    attempt, lineageId, null, created.Create?.Created?.ThreadId, Now()).Attempt;
                RecoveryStoreWriteResult replacementWrite = await _store.RecordReplacementAsync(
                    attempt, recorded, replacementLineage, cancellationToken);
                if (!replacementWrite.Succeeded)
                {
                    await _continuity.CloseSessionAsync(replacementSession);
                    return Failed(attempt, plan, replacementWrite.Diagnostic ?? "The inactive replacement lineage was not persisted.");
                }
                attempt = recorded;
            }
            else if (attempt.ReplacementLineageId is { } persistedLineageId)
            {
                replacementLineage = await _store.ReadLineageAsync(persistedLineageId, cancellationToken);
                if (replacementLineage is null || replacementLineage.AuthorityState != "Inactive")
                {
                    return Failed(attempt, plan, "The persisted inactive replacement lineage is unavailable.");
                }

                replacementReference = new ProviderSessionReference(
                    replacementLineage.Provider, replacementLineage.ProviderSessionId);
                AgentSessionSpec replacementSpec = WithResume(request.FreshSessionSpec, replacementReference.ThreadId);
                SessionResumeResult reopened = await _continuity.ResumeSessionAsync(
                    new SessionResumeRequest(replacementSpec, replacementReference, request.Profile), cancellationToken);
                if (reopened.Outcome != SessionResumeOutcome.SuccessfulResume || reopened.Session is null)
                {
                    return Failed(attempt, plan, "The persisted replacement could not be reopened for reconciliation.");
                }
                replacementSession = reopened.Session;
            }

            if (!mechanism.RequiresContextInjection)
            {
                if (creationResult is null || replacementSession is null
                    || replacementReference is null || replacementLineage is null)
                {
                    return new RecoveryRuntimeResult(
                        RecoveryRuntimeOutcome.UnknownOutcome, null, replacementReference, attempt, plan,
                        RecoveryCompleteness.Unknown, false,
                        "A restarted native replacement requires exact fork reconciliation before activation.");
                }

                RecoveryMechanismValidationResult nativeValidation = await mechanism.ValidateAsync(
                    ExecutionRequest(
                        RecoveryMechanismExecutionPhase.CreateReplacement, request, plan, original,
                        observations, envelope, replacementSession, replacementReference),
                    creationResult,
                    cancellationToken);
                if (!nativeValidation.Valid)
                {
                    return await FailAttemptAsync(
                        attempt, plan, nativeValidation.Unknown ? "UnknownOutcome" : "RecoveryValidationFailed",
                        nativeValidation.Diagnostic ?? "Native replacement validation failed.", cancellationToken);
                }

                RecoveryAttempt nativeCompleted = _journal.Complete(attempt, Now()).Attempt;
                RecoveryStoreWriteResult nativeActivated = await _store.CompleteRecoveryAndActivateAsync(
                    attempt, nativeCompleted, active, replacementLineage, cancellationToken);
                if (!nativeActivated.Succeeded)
                {
                    await _continuity.CloseSessionAsync(replacementSession);
                    return Failed(attempt, plan, nativeActivated.Diagnostic ?? "Atomic native-fork activation failed.");
                }

                return new RecoveryRuntimeResult(
                    mechanism.SuccessOutcome(plan.ExpectedCompleteness), replacementSession, replacementReference,
                    nativeCompleted, plan, plan.ExpectedCompleteness, false, null);
            }

            if (attempt.Status == RecoveryAttemptStatus.ReplacementCreated)
            {
                RecoveryAttempt contextPending = _journal.RecordContextPending(attempt, Now()).Attempt;
                RecoveryStoreWriteResult contextWrite = await _store.CompareAndSwapAttemptAsync(
                    attempt, contextPending, cancellationToken);
                if (!contextWrite.Succeeded)
                {
                    return Failed(attempt, plan, contextWrite.Diagnostic ?? "ContextInjectionPending was not persisted.");
                }
                attempt = contextPending;
            }

            if (attempt.Status != RecoveryAttemptStatus.ContextInjectionPending
                || replacementSession is null || replacementReference is null || replacementLineage is null)
            {
                return Failed(attempt, plan, $"Recovery cannot continue from {attempt.Status}.");
            }

            RecoveryMechanismExecutionRequest injectRequest = ExecutionRequest(
                RecoveryMechanismExecutionPhase.InjectContext, request, plan, original,
                observations, envelope, replacementSession, replacementReference);
            RecoveryMechanismExecutionResult injected = await mechanism.ExecuteAsync(injectRequest, cancellationToken);
            if (injected.Status != RecoveryMechanismExecutionStatus.Succeeded)
            {
                return await RecordMechanismFailureAsync(attempt, plan, injected, cancellationToken);
            }

            RecoveryMechanismValidationResult validation = await mechanism.ValidateAsync(
                injectRequest, injected, cancellationToken);
            if (!validation.Valid)
            {
                return await FailAttemptAsync(
                    attempt, plan, validation.Unknown ? "UnknownOutcome" : "RecoveryValidationFailed",
                    validation.Diagnostic ?? "Recovery validation failed.", cancellationToken);
            }

            RecoveryAttempt completed = _journal.Complete(attempt, Now()).Attempt;
            RecoveryStoreWriteResult activated = await _store.CompleteRecoveryAndActivateAsync(
                attempt, completed, active, replacementLineage, cancellationToken);
            if (!activated.Succeeded)
            {
                await _continuity.CloseSessionAsync(replacementSession);
                return Failed(attempt, plan, activated.Diagnostic ?? "Atomic replacement activation failed.");
            }

            return new RecoveryRuntimeResult(
                mechanism.SuccessOutcome(plan.ExpectedCompleteness), replacementSession, replacementReference,
                completed, plan, plan.ExpectedCompleteness, true, null);
        }
        catch
        {
            if (replacementSession is not null)
            {
                await _continuity.CloseSessionAsync(replacementSession);
            }
            throw;
        }
    }

    private RecoveryMechanismExecutionRequest ExecutionRequest(
        RecoveryMechanismExecutionPhase phase,
        RecoveryRuntimeRequest request,
        RecoveryPlan plan,
        ProviderSessionReference original,
        IReadOnlyList<RecoverySourceObservation> observations,
        RecoveryEnvelopePayload? envelope,
        IAgentSession? session,
        ProviderSessionReference? replacement) =>
        new(
            phase,
            plan,
            new SessionCreateRequest(request.FreshSessionSpec, request.Profile, plan.IdempotencyIdentity),
            original,
            observations,
            envelope?.CanonicalContent,
            envelope?.Marker ?? string.Empty,
            _continuity,
            session,
            replacement);

    private async Task<RecoveryRuntimeResult> RecordMechanismFailureAsync(
        RecoveryAttempt attempt,
        RecoveryPlan plan,
        RecoveryMechanismExecutionResult result,
        CancellationToken cancellationToken)
    {
        string classification = result.Status == RecoveryMechanismExecutionStatus.UnknownOutcome
            ? "UnknownOutcome"
            : result.Failure?.Classification ?? "RecoveryMechanismFailure";
        RecoveryFailure failure = new(
            classification,
            result.Failure?.ProviderMethod ?? plan.Mechanism.Identity,
            result.Failure?.JsonRpcCode,
            plan.ContinuityProfileDigest,
            result.Diagnostic ?? result.Failure?.RedactedDiagnostic ?? classification,
            result.Seed?.Transport?.TurnSubmitted == true);
        RecoveryAttempt updated = result.Status == RecoveryMechanismExecutionStatus.UnknownOutcome
            ? _journal.RecordUnknownOutcome(attempt, failure, Now()).Attempt
            : _journal.Fail(attempt, failure, Now()).Attempt;
        RecoveryStoreWriteResult stored = await _store.CompareAndSwapAttemptAsync(
            attempt, updated, cancellationToken);
        return new RecoveryRuntimeResult(
            result.Status == RecoveryMechanismExecutionStatus.UnknownOutcome
                ? RecoveryRuntimeOutcome.UnknownOutcome
                : RecoveryRuntimeOutcome.FailedClosed,
            null, result.Replacement, stored.Succeeded ? updated : attempt, plan,
            RecoveryCompleteness.Unknown, false,
            stored.Succeeded ? failure.RedactedDiagnostic : stored.Diagnostic);
    }

    private async Task<RecoveryRuntimeResult> FailAttemptAsync(
        RecoveryAttempt attempt,
        RecoveryPlan? plan,
        string classification,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        RecoveryFailure failure = new(
            classification, "recovery", null, attempt.ProfileDigest, diagnostic, false);
        RecoveryAttempt failed = classification == "UnknownOutcome"
            ? _journal.RecordUnknownOutcome(attempt, failure, Now()).Attempt
            : _journal.Fail(attempt, failure, Now()).Attempt;
        RecoveryStoreWriteResult stored = await _store.CompareAndSwapAttemptAsync(
            attempt, failed, cancellationToken);
        return new RecoveryRuntimeResult(
            classification == "UnknownOutcome" ? RecoveryRuntimeOutcome.UnknownOutcome : RecoveryRuntimeOutcome.FailedClosed,
            null, null, stored.Succeeded ? failed : attempt, plan,
            RecoveryCompleteness.Unknown, false,
            stored.Succeeded ? diagnostic : stored.Diagnostic);
    }

    private async Task<RecoveryAttempt> RequiredAttemptAsync(string attemptId, CancellationToken cancellationToken) =>
        await _store.ReadAttemptAsync(attemptId, cancellationToken)
        ?? throw new InvalidOperationException("The just-written recovery attempt could not be reloaded.");

    private static RecoveryFailure Failure(SessionResumeResult result) => new(
        result.Outcome.ToString(),
        result.Failure?.ProviderMethod ?? "thread/resume",
        result.Failure?.JsonRpcCode,
        result.ContinuityProfileDigest,
        result.Failure?.RedactedDiagnostic ?? result.Outcome.ToString(),
        result.Transport.TurnSubmitted);

    private static bool SameSources(
        IReadOnlyList<RecoverySourceDescriptor> planned,
        IReadOnlyList<RecoverySourceObservation> observed) =>
        planned.Select(source => (source.Kind, source.Digest, source.VerifiedBoundary))
            .SequenceEqual(observed.Select(item => (
                item.Descriptor.Kind, item.Descriptor.Digest, item.Descriptor.VerifiedBoundary)));

    private static RecoveryRuntimeResult Failed(RecoveryAttempt attempt, RecoveryPlan? plan, string diagnostic) =>
        new(RecoveryRuntimeOutcome.FailedClosed, null, null, attempt, plan,
            RecoveryCompleteness.Unknown, false, diagnostic);

    private DateTimeOffset Now() => _clock.GetUtcNow();

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static AgentSessionSpec WithResume(AgentSessionSpec source, string threadId) => new(
        source.SessionId,
        source.RepositoryId,
        source.Role,
        source.Sandbox,
        source.Effort,
        source.WorkingDirectory,
        source.StartupOptions,
        threadId,
        source.OperationPermissionProfile);
}
