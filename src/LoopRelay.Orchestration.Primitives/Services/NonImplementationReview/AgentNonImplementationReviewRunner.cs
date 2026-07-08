using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Models.Process;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;
using LoopRelay.Orchestration.Models.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class AgentNonImplementationReviewRunner(
    IAgentRuntime _runtime,
    Repository _repository) : INonImplementationReviewRunner
{
    public NonImplementationReviewRunnerConstraints Capabilities =>
        NonImplementationReviewRunnerConstraints.ReadOnly;

    public async Task<NonImplementationReviewRunnerResponse> RunAsync(
        NonImplementationReviewRunnerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Constraints.EnsureReadOnly();

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

    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";
}
