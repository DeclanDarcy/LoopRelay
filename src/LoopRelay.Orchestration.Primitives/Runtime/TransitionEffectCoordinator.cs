using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Runtime;

public interface ITransitionEffectIntentExecutor
{
    Task<EffectExecutionRecord> ExecuteAsync(
        CanonicalCausalContext causality,
        EffectIdentity effect,
        CancellationToken cancellationToken);
}

public sealed class TransitionEffectExecutorAdapter : LoopRelay.Orchestration.Effects.IEffectExecutor
{
    private readonly ITransitionEffectIntentExecutor _executor;
    private readonly EffectIdentity? _expectedEffect;

    public TransitionEffectExecutorAdapter(ITransitionEffectIntentExecutor executor)
        : this(executor, new EffectExecutorKey("canonical-transition-effect"), null) { }

    public TransitionEffectExecutorAdapter(
        ITransitionEffectIntentExecutor executor,
        EffectExecutorKey key,
        EffectIdentity? expectedEffect)
    {
        _executor = executor;
        Key = key;
        _expectedEffect = expectedEffect;
    }

    public EffectExecutorKey Key { get; }
    public string Version => "1";

    public async Task<EffectExecutionObservation> ExecuteAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        if (_expectedEffect is { } expected &&
            !string.Equals(intent.Target.Identity, expected.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Executor '{Key}' cannot execute effect target '{intent.Target.Identity}'.");
        }
        EffectExecutionRecord result = await _executor.ExecuteAsync(
            intent.Causality,
            new EffectIdentity(intent.Target.Identity),
            cancellationToken);
        EffectLifecycle state = result.Status switch
        {
            EffectExecutionStatus.Succeeded => EffectLifecycle.Succeeded,
            EffectExecutionStatus.Stalled => EffectLifecycle.Stalled,
            EffectExecutionStatus.Failed => EffectLifecycle.Failed,
            EffectExecutionStatus.Unknown or EffectExecutionStatus.PartiallyFailed => EffectLifecycle.Unknown,
            EffectExecutionStatus.Started => EffectLifecycle.Started,
            _ => EffectLifecycle.Pending,
        };
        return new EffectExecutionObservation(
            state,
            result.Explanation,
            result.Evidence,
            "executor-precondition-observation",
            "executor-postcondition-observation",
            state == EffectLifecycle.Succeeded,
            result.Evidence.FirstOrDefault());
    }
}

public sealed class TransitionalFeatureEffectReconciler(IEffectWorkStore _store) : IEffectReconciler
{
    public async Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<EffectWorkItem> plan = await _store.ReadPlanAsync(
            intent.Causality.TransitionRun, cancellationToken);
        string[] children = plan
            .Where(item => item.Intent.Dependencies.Contains(intent.Identity))
            .Select(item => item.Intent.Identity.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return children.Length > 0
            ? new EffectReconciliationObservation(
                EffectReconciliationVerdict.Succeeded,
                "Durable child intents independently confirm feature-effect planning completed.",
                children, "unknown", string.Join(',', children), intent.Identity.Value)
            : new EffectReconciliationObservation(
                EffectReconciliationVerdict.StillUnknown,
                "No durable child intent proves feature-effect planning completed.",
                [intent.Identity.Value], "unknown", "children-missing");
    }
}

public sealed class HumanDecisionEffectReconciler : IEffectReconciler
{
    public Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent,
        CancellationToken cancellationToken) =>
        Task.FromResult(new EffectReconciliationObservation(
            EffectReconciliationVerdict.HumanActionRequired,
            "No independent reconciler is registered for this transitional effect; repeat execution is forbidden.",
            [intent.Identity.Value, intent.Target.Identity],
            "unknown",
            "unknown"));
}

/// <summary>
/// Coordinates by durable transition plan. Work discovery, lifecycle, receipts, and restart
/// reconstruction come exclusively from the canonical effect store.
/// </summary>
public sealed class TransitionEffectCoordinator(
    IEffectWorkStore _store,
    EffectWorker _worker,
    IEffectPlanSettlementStore _settlement) : ITransitionEffectCoordinator
{
    public async Task<TransitionEffectCoordinationResult> CoordinateAsync(
        TransitionRunIdentity transitionRun,
        CancellationToken cancellationToken)
    {
        if (transitionRun.IsEmpty) throw new ArgumentException("Transition-run identity is required.", nameof(transitionRun));
        IReadOnlyList<EffectWorkItem> plan = [];
        for (int pass = 0; pass < 16; pass++)
        {
            await _worker.RunOnceAsync(cancellationToken, includePending: pass == 0);
            plan = await _store.ReadPlanAsync(transitionRun, cancellationToken);
            if (!HasEligibleNewWork(plan)) break;
        }
        if (plan.Count == 0)
        {
            throw new InvalidOperationException($"No durable effect plan exists for transition '{transitionRun}'.");
        }

        string[] evidence = plan.SelectMany(item => item.Events.SelectMany(value => value.Evidence))
            .Concat(plan.Select(item => item.Intent.Identity.Value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (plan.All(item => item.State == EffectLifecycle.Succeeded &&
                item.Receipt is { PostconditionSatisfied: true }))
        {
            bool settled = await _settlement.TrySettleAsync(transitionRun, CancellationToken.None);
            if (settled)
            {
                return new TransitionEffectCoordinationResult(false, false,
                    "All required effect receipts were verified and the transition plan settled.", evidence,
                    RuntimeOutcomeKind.Completed);
            }

            return await RecordOutcomeAsync(
                transitionRun,
                new TransitionEffectCoordinationResult(true, false,
                    "Effect receipts exist but authoritative plan settlement remains pending.", evidence,
                    RuntimeOutcomeKind.EffectsPending));
        }

        if (plan.Any(item => item.State is EffectLifecycle.Unknown or EffectLifecycle.Reconciling or
                EffectLifecycle.HumanActionRequired))
        {
            return await RecordOutcomeAsync(
                transitionRun,
                new TransitionEffectCoordinationResult(true, false,
                    "Required effect work needs reconciliation or a human decision.", evidence,
                    RuntimeOutcomeKind.RecoveryRequired));
        }
        if (plan.Any(item => item.State == EffectLifecycle.Failed))
        {
            return await RecordOutcomeAsync(
                transitionRun,
                new TransitionEffectCoordinationResult(false, true,
                    "A required effect failed and requires explicit retry authority.", evidence,
                    RuntimeOutcomeKind.Failed));
        }
        if (plan.Any(item => item.State == EffectLifecycle.Stalled))
        {
            return await RecordOutcomeAsync(
                transitionRun,
                new TransitionEffectCoordinationResult(false, false,
                    "A required effect stalled and requires explicit retry authority.", evidence,
                    RuntimeOutcomeKind.Stalled));
        }

        return await RecordOutcomeAsync(
            transitionRun,
            new TransitionEffectCoordinationResult(true, false,
                "Required effect work remains pending.", evidence, RuntimeOutcomeKind.EffectsPending));
    }

    private static bool HasEligibleNewWork(IReadOnlyList<EffectWorkItem> plan)
    {
        HashSet<EffectIntentIdentity> succeeded = plan
            .Where(item => item.State == EffectLifecycle.Succeeded &&
                item.Receipt is { PostconditionSatisfied: true })
            .Select(item => item.Intent.Identity)
            .ToHashSet();
        return plan.Any(item => item.State == EffectLifecycle.Planned &&
            item.Intent.Dependencies.All(succeeded.Contains));
    }

    private async Task<TransitionEffectCoordinationResult> RecordOutcomeAsync(
        TransitionRunIdentity transitionRun,
        TransitionEffectCoordinationResult result)
    {
        await _settlement.RecordOutcomeAsync(
            transitionRun,
            result.Outcome ?? throw new InvalidOperationException("Effect coordination outcomes must be explicit."),
            result.Explanation,
            CancellationToken.None);
        return result;
    }
}
