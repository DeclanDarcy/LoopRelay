using LoopRelay.Orchestration.Models;

namespace LoopRelay.Orchestration.Runtime;

public interface IExecutionAuthorizationResolver
{
    Task<ResolvedRuntimeProfile> ResolveAsync(
        ExecutionAuthorization authorization,
        CancellationToken cancellationToken = default);
}

/// <summary>Resolves only durable Policy Authority output; raw recommendations are not an input.</summary>
public sealed class ExecutionAuthorizationResolver(
    IRuntimeProfileEvaluationStore _evaluations,
    IResolvedRuntimeProfileStore _profiles) : IExecutionAuthorizationResolver
{
    public async Task<ResolvedRuntimeProfile> ResolveAsync(
        ExecutionAuthorization authorization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        RuntimeProfileEvaluation evaluation = await _evaluations.ReadAsync(
            authorization.PolicyEvaluation,
            cancellationToken)
            ?? throw new InvalidOperationException("Execution policy evaluation was not found.");
        if (evaluation.DecisionProduct != authorization.DecisionProduct ||
            evaluation.EffectiveProfile.Identity != authorization.RuntimeProfile ||
            evaluation.Policy != authorization.Policy ||
            evaluation.ProviderCapabilities.Identity != authorization.ProviderCapabilities ||
            evaluation.Recommendation != authorization.Recommendation)
        {
            throw new InvalidOperationException("Execution authorization does not match its policy evaluation.");
        }

        ResolvedRuntimeProfile profile = await _profiles.ReadAsync(
            authorization.RuntimeProfile,
            cancellationToken)
            ?? throw new InvalidOperationException("Resolved runtime profile was not found.");
        if (profile.Identity != evaluation.EffectiveProfile.Identity || profile != evaluation.EffectiveProfile)
        {
            throw new InvalidOperationException("Resolved runtime profile differs from durable policy evaluation.");
        }

        return profile;
    }
}
