using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Chaining;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Runtime;

public interface ITransitionEffectIntentExecutor
{
    Task<EffectExecutionRecord> ExecuteAsync(
        CanonicalCausalContext causality,
        EffectIdentity effect,
        CancellationToken cancellationToken);
}

public interface ITransitionEffectIntentStateStore
{
    Task RecordStateAsync(
        TransitionRunIdentity transitionRun,
        EffectIdentity effect,
        EffectExecutionStatus status,
        string? failure,
        CancellationToken cancellationToken);
}

/// <summary>
/// Executes already-durable effect intents in declared order. Thrown external calls are recorded
/// as Unknown and require reconciliation before another execution claim.
/// </summary>
public sealed class TransitionEffectCoordinator(
    ITransitionEffectIntentExecutor _executor,
    ITransitionEffectIntentStateStore _states) : ITransitionEffectCoordinator
{
    public async Task<TransitionEffectCoordinationResult> CoordinateAsync(
        TransitionRuntimeResult attempt,
        CanonicalTransitionExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        if (attempt.TransitionRun is not { } transitionRun || attempt.Attempt is not { } attemptId)
        {
            throw new InvalidOperationException("Effect coordination requires a causally identified attempt.");
        }

        var causality = new CanonicalCausalContext(
            executionContext.Workspace,
            executionContext.Run,
            executionContext.WorkflowInstance,
            transitionRun,
            attemptId);
        IReadOnlyList<EffectExecutionRecord> planned = attempt.Effects?.Effects ?? [];
        var evidence = new List<string>();
        foreach (EffectExecutionRecord intent in planned)
        {
            await _states.RecordStateAsync(
                transitionRun, intent.Effect, EffectExecutionStatus.Started, null, cancellationToken);
            try
            {
                EffectExecutionRecord result =
                    await _executor.ExecuteAsync(causality, intent.Effect, cancellationToken);
                await _states.RecordStateAsync(
                    transitionRun,
                    intent.Effect,
                    result.Status,
                    result.Status == EffectExecutionStatus.Succeeded ? null : result.Explanation,
                    CancellationToken.None);
                evidence.AddRange(result.Evidence);
                if (result.Status != EffectExecutionStatus.Succeeded)
                {
                    return new TransitionEffectCoordinationResult(
                        RequiredEffectsPending: result.Status is EffectExecutionStatus.Planned or EffectExecutionStatus.Unknown,
                        Failed: result.Status is EffectExecutionStatus.Failed or EffectExecutionStatus.PartiallyFailed,
                        result.Explanation,
                        evidence);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                await _states.RecordStateAsync(
                    transitionRun,
                    intent.Effect,
                    EffectExecutionStatus.Unknown,
                    exception.Message,
                    CancellationToken.None);
                return new TransitionEffectCoordinationResult(
                    RequiredEffectsPending: true,
                    Failed: false,
                    "An effect call has an unknown outcome and must be reconciled before retry.",
                    evidence.Append(exception.GetType().Name).ToArray());
            }
        }

        return new TransitionEffectCoordinationResult(
            RequiredEffectsPending: false,
            Failed: false,
            "All required effect intents completed.",
            evidence);
    }
}
