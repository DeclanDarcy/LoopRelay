using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Models;

namespace LoopRelay.Orchestration.Policy;

public sealed class ExecutionRecommendationPolicyService(
    ExecutionRecommendationPolicyEvaluator _evaluator,
    IRuntimeProfileEvaluationStore _evaluations)
{
    public async Task<(RuntimeProfileEvaluation Evaluation, ExecutionAuthorization Authorization)> AuthorizeAsync(
        ExecutionRecommendationEvaluationRequest request,
        RenderedPromptFactIdentity executionPrompt,
        ConsumedInputManifestIdentity consumedInputManifest,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken = default)
    {
        RuntimeProfileEvaluation evaluation = _evaluator.Evaluate(request);
        await _evaluations.AppendAsync(evaluation, cancellationToken);
        var authorization = new ExecutionAuthorization(
            ExecutionAuthorizationIdentity.New(),
            request.CurrentDecisionProduct,
            evaluation.EffectiveProfile.Identity,
            evaluation.Identity,
            executionPrompt,
            consumedInputManifest,
            causality);
        return (evaluation, authorization);
    }
}
