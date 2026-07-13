using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Orchestration.Effects;

public sealed record EffectWorkerResult(
    int Discovered,
    int Leased,
    int Succeeded,
    int Pending,
    int RecoveryRequired,
    IReadOnlyList<EffectIntentIdentity> Unsettled);

/// <summary>
/// Drives durable effect work only from the store. It never relies on an in-memory transition
/// result, and it reconciles uncertain work before any repeat outward call.
/// </summary>
public sealed class EffectWorker(
    string _workerIdentity,
    IEffectWorkStore _store,
    IEffectExecutorRegistry _executors,
    IEffectReconciler _reconciler,
    TimeSpan _leaseDuration,
    int _scanLimit = 128,
    ICanonicalRecoveryCaseRecorder? _recoveryCases = null)
{
    public async Task<EffectWorkerResult> RunOnceAsync(
        CancellationToken cancellationToken = default,
        bool includePending = true,
        IReadOnlySet<EffectIntentIdentity>? only = null)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        IReadOnlyList<EffectWorkItem> discovered = await _store.ScanUnsettledAsync(_scanLimit, now, cancellationToken);
        var settled = new HashSet<EffectIntentIdentity>();
        var unsettled = new List<EffectIntentIdentity>();
        int leased = 0;
        int succeeded = 0;
        int pending = 0;
        int recovery = 0;

        foreach (EffectWorkItem item in discovered
            .Where(item => only is null || only.Contains(item.Intent.Identity))
            .OrderBy(value => value.Intent.Order)
            .ThenBy(value => value.Intent.PlannedAt))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!includePending && item.State == EffectLifecycle.Pending)
            {
                pending++;
                unsettled.Add(item.Intent.Identity);
                continue;
            }
            if (!await DependenciesSettledAsync(item.Intent, settled, cancellationToken))
            {
                pending++;
                unsettled.Add(item.Intent.Identity);
                continue;
            }

            EffectLease? lease = await _store.TryLeaseAsync(
                item.Intent.Identity,
                item.RowVersion,
                _workerIdentity,
                now,
                _leaseDuration,
                cancellationToken);
            if (lease is null)
            {
                continue;
            }

            leased++;
            EffectLifecycle previous = lease.PreviousState;
            EffectWorkItem current = (await _store.ReadAsync(item.Intent.Identity, cancellationToken))!;
            if (previous is EffectLifecycle.Started or EffectLifecycle.Unknown or EffectLifecycle.Reconciling)
            {
                current = await ReconcileAsync(current, cancellationToken);
                if (current.State == EffectLifecycle.Succeeded)
                {
                    settled.Add(current.Intent.Identity);
                    succeeded++;
                }
                else
                {
                    unsettled.Add(current.Intent.Identity);
                    recovery++;
                    await RecordRecoveryAsync(
                        current.Intent,
                        ["effect-reconciliation-unsettled", $"state:{current.State}"],
                        evidenceComplete: current.State != EffectLifecycle.Unknown);
                }
                continue;
            }

            if (previous is not (EffectLifecycle.Planned or EffectLifecycle.Pending or EffectLifecycle.RetryAuthorized or EffectLifecycle.Leased))
            {
                unsettled.Add(current.Intent.Identity);
                recovery++;
                continue;
            }

            current = await _store.AppendLifecycleAsync(
                current.Intent.Identity,
                current.RowVersion,
                EffectLifecycle.Started,
                _workerIdentity,
                "Outward effect execution started.",
                [],
                DateTimeOffset.UtcNow,
                CancellationToken.None);
            try
            {
                IEffectExecutor executor = _executors.Resolve(current.Intent.Executor, current.Intent.ExecutorVersion);
                EffectExecutionObservation observation = await executor.ExecuteAsync(current.Intent, cancellationToken);
                if (observation.State == EffectLifecycle.Succeeded && observation.PostconditionSatisfied)
                {
                    current = await _store.RecordReceiptAsync(
                        current.Intent.Identity,
                        current.RowVersion,
                        Receipt(current.Intent, observation),
                        _workerIdentity,
                        CancellationToken.None);
                    settled.Add(current.Intent.Identity);
                    succeeded++;
                }
                else
                {
                    EffectLifecycle state = observation.State == EffectLifecycle.Succeeded
                        ? EffectLifecycle.Unknown
                        : observation.State;
                    current = await _store.AppendLifecycleAsync(
                        current.Intent.Identity,
                        current.RowVersion,
                        state,
                        _workerIdentity,
                        observation.Explanation,
                        observation.Evidence,
                        DateTimeOffset.UtcNow,
                        CancellationToken.None);
                    unsettled.Add(current.Intent.Identity);
                    if (state == EffectLifecycle.Pending) pending++; else recovery++;
                    if (state is EffectLifecycle.Unknown or EffectLifecycle.HumanActionRequired)
                    {
                        await RecordRecoveryAsync(current.Intent, observation.Evidence, evidenceComplete: true);
                    }
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                await _store.AppendLifecycleAsync(
                    current.Intent.Identity,
                    current.RowVersion,
                    EffectLifecycle.Unknown,
                    _workerIdentity,
                    "Effect execution ended without a trustworthy observation.",
                    [exception.GetType().Name, exception.Message],
                    DateTimeOffset.UtcNow,
                    CancellationToken.None);
                unsettled.Add(current.Intent.Identity);
                recovery++;
                await RecordRecoveryAsync(
                    current.Intent,
                    [exception.GetType().Name, exception.Message],
                    evidenceComplete: true);
            }
        }

        return new EffectWorkerResult(discovered.Count, leased, succeeded, pending, recovery, unsettled);
    }

    private async Task RecordRecoveryAsync(
        EffectIntent intent,
        IReadOnlyList<string> evidence,
        bool evidenceComplete)
    {
        if (_recoveryCases is null) return;
        var subject = new RecoveryCausalSubject(
            intent.Causality,
            EffectPlanIdentity: intent.Identity.Value);
        await _recoveryCases.RecordAsync(
            RecoveryScopeKind.EffectPlan,
            subject,
            new RecoveryDurableFacts(
                RecoveryScopeKind.EffectPlan, subject, evidenceComplete, Corrupt: false,
                Authorized: true, ValidInFlightCorrelation: false,
                OutwardStarted: true, OutwardAccepted: true, ProviderOutcomeUnknown: false,
                TerminalProviderResult: false, RawOutputDurable: false, OutputPromoted: false,
                ExplicitFailure: false, ExplicitCancellation: false, RecoveryCancellationBoundary.None,
                RequiredEffects: 1, SucceededEffects: 0,
                CompletionClosureStarted: false, CompletionClosureSettled: false,
                Evidence: evidence),
            CancellationToken.None);
    }

    private async Task<EffectWorkItem> ReconcileAsync(EffectWorkItem current, CancellationToken cancellationToken)
    {
        current = await _store.AppendLifecycleAsync(
            current.Intent.Identity,
            current.RowVersion,
            EffectLifecycle.Reconciling,
            _workerIdentity,
            "Independent postcondition reconciliation started.",
            [],
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        EffectReconciliationObservation observation = await _reconciler.ReconcileAsync(current.Intent, cancellationToken);
        await _store.RecordReconciliationAsync(
            current.Intent.Identity,
            current.RowVersion,
            observation,
            _workerIdentity,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
        if (observation.Verdict == EffectReconciliationVerdict.Succeeded)
        {
            var execution = new EffectExecutionObservation(
                EffectLifecycle.Succeeded,
                observation.Explanation,
                observation.Evidence,
                observation.BeforeFacts,
                observation.AfterFacts,
                true,
                observation.ExternalCorrelation);
            return await _store.RecordReceiptAsync(
                current.Intent.Identity,
                current.RowVersion,
                Receipt(current.Intent, execution),
                _workerIdentity,
                CancellationToken.None);
        }

        EffectLifecycle state = observation.Verdict switch
        {
            EffectReconciliationVerdict.NotApplied => EffectLifecycle.RetryAuthorized,
            EffectReconciliationVerdict.HumanActionRequired => EffectLifecycle.HumanActionRequired,
            _ => EffectLifecycle.Unknown,
        };
        return await _store.AppendLifecycleAsync(
            current.Intent.Identity,
            current.RowVersion,
            state,
            _workerIdentity,
            observation.Explanation,
            observation.Evidence,
            DateTimeOffset.UtcNow,
            CancellationToken.None);
    }

    private async Task<bool> DependenciesSettledAsync(
        EffectIntent intent,
        HashSet<EffectIntentIdentity> settledThisRun,
        CancellationToken cancellationToken)
    {
        foreach (EffectIntentIdentity dependency in intent.Dependencies)
        {
            if (settledThisRun.Contains(dependency)) continue;
            EffectWorkItem? item = await _store.ReadAsync(dependency, cancellationToken);
            if (item?.State != EffectLifecycle.Succeeded || item.Receipt is null || !item.Receipt.PostconditionSatisfied)
            {
                return false;
            }
        }
        return true;
    }

    private static EffectReceipt Receipt(EffectIntent intent, EffectExecutionObservation observation) => new(
        EffectReceiptIdentity.New(),
        intent.Identity,
        intent.Executor,
        intent.ExecutorVersion,
        intent.Target.Identity,
        observation.BeforeFacts,
        observation.AfterFacts,
        observation.PostconditionSatisfied,
        observation.ExternalCorrelation,
        observation.Evidence,
        DateTimeOffset.UtcNow);
}
