using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationReview;
using LoopRelay.Orchestration.Runtime;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class AgentNonImplementationReviewRunner(
    IAgentRuntime _runtime,
    Repository _repository,
    IRenderedPromptStore? _renderedPromptStore = null,
    string? _policyIdentity = null,
    PromptExecutionContext? _executionContext = null) : INonImplementationReviewRunner
{
    public NonImplementationReviewRunnerConstraints Capabilities =>
        NonImplementationReviewRunnerConstraints.ReadOnly;

    public async Task<NonImplementationReviewRunnerResponse> RunAsync(
        NonImplementationReviewRunnerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Constraints.EnsureReadOnly();

        // The payload is composed upstream (template plus caller-injected sections); the fact
        // records the exact sent text so payload drift is observable in evidence even though
        // the payload's template-hash linkage joins at M7 gateway capture.
        await TryAppendRenderedPromptAsync(request.PromptName, request.PromptPayload);
        AgentTurnResult result = await _runtime.RunOneShotAsync(
            ReadOnlyReviewSpec(_repository),
            request.PromptPayload,
            onChunk: null,
            cancellationToken);

        if (result.State != AgentTurnState.Completed)
        {
            throw new NonImplementationReviewRunnerException(
                WithDiagnostics(
                    $"{request.PromptName} non-implementation review turn ended in state {result.State}.",
                    result.Diagnostics));
        }

        return new NonImplementationReviewRunnerResponse(result.Output);
    }

    private static AgentSessionSpec ReadOnlyReviewSpec(Repository repository) =>
        new(
            SessionIdentity.New(),
            repository.Id.ToString("N"),
            SessionRole.Planning,
            new SandboxProfile("read-only", CanWriteWorkspace: false, CanAccessNetwork: false, RequiresApproval: false),
            new EffortProfile(AgentEffortLevel.High, "xhigh"),
            repository.Path);

    private async Task TryAppendRenderedPromptAsync(string promptIdentity, string renderedText)
    {
        if (_renderedPromptStore is null)
        {
            return;
        }

        try
        {
            await _renderedPromptStore.AppendAsync(
                new RenderedPromptCapture(
                    _executionContext?.TransitionRunId ?? string.Empty,
                    _executionContext?.AttemptId,
                    promptIdentity,
                    null,
                    renderedText,
                    [],
                    _policyIdentity,
                    DateTimeOffset.UtcNow),
                CancellationToken.None);
        }
        catch
        {
            // Rendered-prompt persistence is supporting evidence; failing to append must not fail the review.
        }
    }

    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}
