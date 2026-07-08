using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Core.Repositories;
using LoopRelay.Orchestration.Abstractions.NonImplementationReview;

namespace LoopRelay.Orchestration.Services.NonImplementationReview;

public sealed class NonImplementationReviewRunnerException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

public sealed class AgentNonImplementationReviewRunner(
    IAgentRuntime runtime,
    Repository repository) : INonImplementationReviewRunner
{
    public NonImplementationReviewRunnerConstraints Capabilities =>
        NonImplementationReviewRunnerConstraints.ReadOnly;

    public async Task<NonImplementationReviewRunnerResponse> RunAsync(
        NonImplementationReviewRunnerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Constraints.EnsureReadOnly();

        AgentTurnResult result = await runtime.RunOneShotAsync(
            ReadOnlyReviewSpec(repository),
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
