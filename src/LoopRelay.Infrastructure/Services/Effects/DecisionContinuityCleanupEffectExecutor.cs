using System.Text.Json;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Effects;
using LoopRelay.Orchestration.Recovery;

namespace LoopRelay.Infrastructure.Services.Effects;

public sealed class DecisionContinuityCleanupEffectExecutor(Repository _repository) : IEffectExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public EffectExecutorKey Key => WorkspaceEffectExecutorKeys.DecisionContinuityCleanup;
    public string Version => "1";

    public async Task<EffectExecutionObservation> ExecuteAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        DecisionContinuityCleanupPayload payload = Parse(intent);
        var store = Store();
        DecisionContinuityStatusSnapshot before = await store.ReadStatusAsync(cancellationToken);
        if (before.Active is not null)
        {
            RecoveryStoreWriteResult result = await store.RetireScopeAsync(
                before.Active.ScopeId, before.Active.RowVersion, cancellationToken);
            if (!result.Succeeded)
            {
                return new EffectExecutionObservation(
                    EffectLifecycle.Unknown,
                    $"Decision continuity retirement could not be confirmed: {result.Diagnostic}",
                    [before.Active.ScopeId, payload.CausalReference],
                    before.Active.ScopeId,
                    "unknown",
                    false);
            }
        }
        DecisionContinuityStatusSnapshot after = await store.ReadStatusAsync(cancellationToken);
        bool satisfied = after.Active is null;
        return new EffectExecutionObservation(
            satisfied ? EffectLifecycle.Succeeded : EffectLifecycle.Failed,
            satisfied ? "Decision continuity scope is retired." : "Decision continuity scope remains active.",
            [payload.CausalReference],
            before.Active?.ScopeId ?? "none",
            after.Active?.ScopeId ?? "none",
            satisfied,
            payload.CausalReference);
    }

    internal static DecisionContinuityCleanupPayload Parse(EffectIntent intent) =>
        JsonSerializer.Deserialize<DecisionContinuityCleanupPayload>(intent.TypedPayload, JsonOptions)
        ?? throw new InvalidOperationException("Decision-continuity cleanup payload is invalid.");

    internal CanonicalDecisionRecoveryStore Store() =>
        new(_repository, new SqliteRecoveryStore(_repository));
}

public sealed class DecisionContinuityCleanupReconciler(Repository repository) : IEffectReconciler
{
    private readonly DecisionContinuityCleanupEffectExecutor _observer = new(repository);

    public async Task<EffectReconciliationObservation> ReconcileAsync(
        EffectIntent intent,
        CancellationToken cancellationToken)
    {
        DecisionContinuityCleanupPayload payload = DecisionContinuityCleanupEffectExecutor.Parse(intent);
        DecisionContinuityStatusSnapshot status = await _observer.Store().ReadStatusAsync(cancellationToken);
        return status.Active is null
            ? new EffectReconciliationObservation(
                EffectReconciliationVerdict.Succeeded,
                "Decision continuity authority independently confirms no active scope.",
                [payload.CausalReference],
                "unknown",
                "none",
                payload.CausalReference)
            : new EffectReconciliationObservation(
                EffectReconciliationVerdict.NotApplied,
                "A decision continuity scope remains active.",
                [status.Active.ScopeId, payload.CausalReference],
                "unknown",
                status.Active.ScopeId);
    }
}
