using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Models;
using LoopRelay.Orchestration.Workflows;

namespace LoopRelay.Orchestration.Policy;

public sealed class ExecutionRecommendationPolicyService(
    ExecutionRecommendationPolicyEvaluator _evaluator,
    IRuntimeProfileEvaluationStore _evaluations)
{
    public async Task<(RuntimeProfileEvaluation Evaluation, ExecutionAuthorization Authorization)> AuthorizeAsync(
        ExecutionRecommendationEvaluationRequest request,
        PromptPolicyProfileIdentity promptPolicyProfile,
        string catalogIdentity,
        WorkflowIdentity catalogWorkflow,
        WorkflowTransitionIdentity catalogTransition,
        string permissionCeilings,
        RenderedPromptFactIdentity executionPrompt,
        ConsumedInputManifestIdentity consumedInputManifest,
        CanonicalCausalContext causality,
        CancellationToken cancellationToken = default)
    {
        RuntimeProfileEvaluation evaluation = _evaluator.Evaluate(request);
        await _evaluations.AppendAsync(evaluation, cancellationToken);
        string authorizationDocument = JsonSerializer.Serialize(new
        {
            decision = request.CurrentDecisionProduct.Value,
            recommendation = request.Recommendation?.Identity.Value,
            policy = request.Policy.Value,
            evaluation = evaluation.Identity.Value,
            runtime = evaluation.EffectiveProfile.Identity.Value,
            providerCapabilities = request.ProviderCapabilities.Identity.Value,
            promptPolicy = promptPolicyProfile.Value,
            catalogIdentity,
            workflow = catalogWorkflow.Value,
            transition = catalogTransition.Value,
            permissionCeilings,
            prompt = executionPrompt.Value,
            inputManifest = consumedInputManifest.Value,
            workspace = causality.Workspace.Value,
            run = causality.Run.Value,
            workflowInstance = causality.WorkflowInstance.Value,
            transitionRun = causality.TransitionRun.Value,
            attempt = causality.Attempt.Value,
        });
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(authorizationDocument)));
        var authorization = new ExecutionAuthorization(
            new ExecutionAuthorizationIdentity($"execauth_{hash}"),
            request.CurrentDecisionProduct,
            evaluation.EffectiveProfile.Identity,
            evaluation.Identity,
            request.Recommendation?.Identity,
            request.Policy,
            request.ProviderCapabilities.Identity,
            promptPolicyProfile,
            catalogIdentity,
            catalogWorkflow,
            catalogTransition,
            permissionCeilings,
            executionPrompt,
            consumedInputManifest,
            causality);
        return (evaluation, authorization);
    }
}
