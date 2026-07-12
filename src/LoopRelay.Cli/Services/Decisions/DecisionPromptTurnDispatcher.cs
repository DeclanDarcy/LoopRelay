using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Models.Streams;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Identity;
using LoopRelay.Orchestration.Runtime;
using LoopRelay.Orchestration.Services;

namespace LoopRelay.Cli.Services.Decisions;

internal interface IDecisionPromptTurnDispatcher
{
    Task<DecisionPromptTurnResult> DispatchAsync(
        IAgentSession session,
        string promptIdentity,
        string? templateSourceHash,
        string renderedTemplate,
        IReadOnlyList<ConsumedInputFile> consumedInputs,
        Func<AgentStreamChunk, Task>? onChunk,
        CancellationToken cancellationToken);
}

internal sealed record DecisionPromptTurnResult(
    AgentTurnResult Result,
    AgentSessionIdentity Session,
    TurnIdentity Turn,
    PromptDispatchIdentity Dispatch,
    RenderedPromptFactIdentity Prompt,
    CanonicalCausalContext Causality);

/// <summary>
/// Session-aware Prompt Authority adapter. Every warm or scoped session turn is composed,
/// persisted, authorized, and lifecycle-recorded before the provider receives its bytes.
/// </summary>
internal sealed class CanonicalDecisionPromptTurnDispatcher(
    IRenderedPromptStore _promptStore,
    IRenderedPromptFactReader _promptReader,
    IPromptDispatchLifecycleStore _lifecycle,
    IPromptComposer _composer,
    PromptPolicyProfile _policyProfile,
    PromptDispatchAuthorization _baseAuthorization) : IDecisionPromptTurnDispatcher
{
    public async Task<DecisionPromptTurnResult> DispatchAsync(
        IAgentSession session,
        string promptIdentity,
        string? templateSourceHash,
        string renderedTemplate,
        IReadOnlyList<ConsumedInputFile> consumedInputs,
        Func<AgentStreamChunk, Task>? onChunk,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        var sessionIdentity = new AgentSessionIdentity(session.SessionId.ToString());
        TurnIdentity turnIdentity = TurnIdentity.New();
        var authorization = new PromptDispatchAuthorization(
            _baseAuthorization.Causality,
            _baseAuthorization.Policy,
            _baseAuthorization.PolicyProfile,
            _baseAuthorization.RuntimeProfile,
            _baseAuthorization.Transition,
            _baseAuthorization.InputSnapshotHash,
            sessionIdentity,
            turnIdentity);
        PromptComposition composition = _composer.Compose(
            new PromptTemplateIdentity(promptIdentity),
            templateSourceHash,
            authorization.Policy,
            _policyProfile,
            ConsumedInputManifestIdentity.New(),
            consumedInputs,
            new Dictionary<string, string>(),
            renderedTemplate);
        var transport = new SessionTransport(session, onChunk);
        var gateway = new PromptDispatchGateway(
            _promptStore,
            _lifecycle,
            new LoadingPromptRuntimeDispatcher(_promptReader, transport));

        PreparedPromptDispatch prepared = await gateway.PrepareAsync(
            composition,
            authorization,
            cancellationToken);
        await gateway.DispatchAsync(prepared, cancellationToken);
        AgentTurnResult result = transport.Result
            ?? throw new InvalidOperationException("Provider transport returned no session-turn evidence.");
        return new DecisionPromptTurnResult(
            result,
            sessionIdentity,
            turnIdentity,
            prepared.Dispatch.Dispatch,
            prepared.Dispatch.Prompt,
            authorization.Causality);
    }

    private sealed class SessionTransport(
        IAgentSession _session,
        Func<AgentStreamChunk, Task>? _onChunk) : IProviderPromptTransport
    {
        public AgentTurnResult? Result { get; private set; }

        public async Task<PromptExecutionResult> DispatchAsync(
            PersistedRenderedPromptFact prompt,
            AuthorizedPromptDispatch dispatch,
            CancellationToken cancellationToken)
        {
            Result = await _session.RunTurnAsync(
                prompt.Fact.RenderedContent,
                _onChunk,
                cancellationToken);
            PromptExecutionStatus status = Result.State switch
            {
                AgentTurnState.Completed => PromptExecutionStatus.Completed,
                AgentTurnState.Canceled => PromptExecutionStatus.Cancelled,
                _ => PromptExecutionStatus.Failed,
            };
            return new PromptExecutionResult(
                status,
                Result.Output,
                TimeSpan.Zero,
                new Dictionary<string, string>
                {
                    ["session_id"] = dispatch.Authorization.Session?.Value ?? string.Empty,
                    ["turn_id"] = dispatch.Authorization.Turn?.Value ?? string.Empty,
                    ["provider_turn_id"] = Result.ProviderTurnId ?? string.Empty,
                },
                Result.Diagnostics);
        }
    }
}
