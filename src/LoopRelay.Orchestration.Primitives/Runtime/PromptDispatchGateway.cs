using LoopRelay.Core.Models.Identity;

namespace LoopRelay.Orchestration.Runtime;

/// <summary>
/// Prompt Authority persists immutable prompt bytes and dispatch authorization before handing an
/// identity-only request to Runtime Authority.
/// </summary>
public sealed class PromptDispatchGateway(
    IRenderedPromptStore _prompts,
    IPromptDispatchLifecycleStore _dispatches,
    IPromptRuntimeDispatcher _runtime) : IPromptDispatchGateway
{
    public async Task<PreparedPromptDispatch> PrepareAsync(
        PromptComposition composition,
        PromptDispatchAuthorization authorization,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(authorization);
        if (composition.Policy != authorization.Policy ||
            composition.PolicyProfile != authorization.PolicyProfile)
        {
            throw new InvalidOperationException(
                "Prompt composition and dispatch authorization reference different policy identities.");
        }

        var fact = new RenderedPromptFact(
            RenderedPromptFactIdentity.New(),
            authorization.Causality,
            composition.RenderedContent,
            RenderedPromptFact.ComputeContentHash(composition.RenderedContent),
            composition.Template,
            composition.TemplateSourceHash,
            composition.Policy,
            composition.PolicyProfile,
            composition.ConsumedInputManifest,
            composition.ConsumedInputs,
            DateTimeOffset.UtcNow,
            composition.RenderedEncoding);
        PersistedRenderedPromptFact persisted = await _prompts.AppendAsync(fact, cancellationToken);
        var dispatch = new AuthorizedPromptDispatch(
            PromptDispatchIdentity.New(),
            fact.Identity,
            persisted.PersistenceIdentity,
            authorization);
        await AppendAsync(dispatch, PromptDispatchState.Planned,
            [composition.Identity.Value, persisted.PersistenceIdentity.Value], cancellationToken);
        await AppendAsync(dispatch, PromptDispatchState.Authorized,
            [authorization.Policy.Value, authorization.RuntimeProfile.Value], cancellationToken);
        return new PreparedPromptDispatch(persisted, dispatch);
    }

    public async Task<PromptExecutionResult> DispatchAsync(
        PreparedPromptDispatch prepared,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        await AppendAsync(prepared.Dispatch, PromptDispatchState.Started, [], cancellationToken);
        try
        {
            PromptExecutionResult result =
                await _runtime.DispatchAsync(prepared.Dispatch, cancellationToken);
            PromptDispatchState state = result.Status switch
            {
                PromptExecutionStatus.Completed => PromptDispatchState.Observed,
                PromptExecutionStatus.Cancelled => PromptDispatchState.Cancelled,
                _ => PromptDispatchState.Failed,
            };
            await AppendAsync(
                prepared.Dispatch,
                state,
                result.FailureMessage is null ? [] : [result.FailureMessage],
                CancellationToken.None);
            return result;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            await AppendAsync(
                prepared.Dispatch,
                PromptDispatchState.Unknown,
                [exception.GetType().Name],
                CancellationToken.None);
            throw new PromptDispatchUnknownException(prepared.Dispatch, exception);
        }
    }

    private Task AppendAsync(
        AuthorizedPromptDispatch dispatch,
        PromptDispatchState state,
        IReadOnlyList<string> evidence,
        CancellationToken cancellationToken) =>
        _dispatches.AppendAsync(
            new PromptDispatchLifecycleEvent(
                dispatch.Dispatch,
                dispatch.Prompt,
                dispatch.Persistence,
                dispatch.Authorization.Causality,
                dispatch.Authorization.RuntimeProfile,
                dispatch.Authorization.Session,
                dispatch.Authorization.Turn,
                state,
                DateTimeOffset.UtcNow,
                evidence),
            cancellationToken);
}
