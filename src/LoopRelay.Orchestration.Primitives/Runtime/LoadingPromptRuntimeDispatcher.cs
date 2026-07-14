namespace LoopRelay.Orchestration.Runtime;

public interface IProviderPromptTransport
{
    Task<PromptExecutionResult> DispatchAsync(
        PersistedRenderedPromptFact prompt,
        AuthorizedPromptDispatch dispatch,
        CancellationToken cancellationToken);
}

/// <summary>
/// Runtime Authority resolves immutable bytes from the prompt ledger immediately before provider
/// transport. The identity-only dispatch cannot replace or mutate provider-visible prompt text.
/// </summary>
public sealed class LoadingPromptRuntimeDispatcher(
    IRenderedPromptFactReader _prompts,
    IProviderPromptTransport _transport) : IPromptExecutor
{
    public async Task<PromptExecutionResult> DispatchAsync(
        AuthorizedPromptDispatch dispatch,
        CancellationToken cancellationToken)
    {
        PersistedRenderedPromptFact prompt = await _prompts.ReadAsync(dispatch.Prompt, cancellationToken)
            ?? throw new InvalidOperationException($"Rendered prompt fact '{dispatch.Prompt}' was not found.");
        prompt.Fact.Causality.RequireSameAttempt(dispatch.Authorization.Causality, nameof(dispatch));
        if (prompt.Fact.PolicyIdentity != dispatch.Authorization.Policy ||
            prompt.Fact.PolicyProfileIdentity != dispatch.Authorization.PolicyProfile ||
            prompt.PersistenceIdentity != dispatch.Persistence)
        {
            throw new InvalidOperationException("Rendered prompt persistence does not match dispatch authorization.");
        }

        return await _transport.DispatchAsync(prompt, dispatch, cancellationToken);
    }
}
