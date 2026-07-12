using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models.Sessions;
using LoopRelay.Agents.Primitives.Sessions;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.Hitl;
using LoopRelay.Plan.Cli.Abstractions;
using LoopRelay.Plan.Cli.Models;
using LoopRelay.Plan.Cli.Services.Cli;
using LoopRelay.Plan.Cli.Services.PlanArtifactOperations;

namespace LoopRelay.Plan.Cli.Services.Execution;

/// <summary>
/// Pipeline steps 2-5: a persistent two-turn codex planning session held open across the review sandwiched
/// between its two turns (WritePlan, then RevisePlan on the SAME warm session — the second turn sends only its
/// delta, mirroring ExecutionStep's held-open two-turn pattern). Each turn is followed by a deterministic
/// filesystem gate: .agents/plan.md must exist and be non-whitespace after either turn, or the session is
/// closed and a PlanStepException is thrown with the agent stderr tail appended (the WithDiagnostics idiom
/// copied from DecisionSession). The session is closed exactly once regardless of how many turns run or fail.
/// </summary>
internal sealed class PlanSession(
    IAgentRuntime _runtime,
    PlanArtifacts _artifacts,
    ILoopConsole _console,
    Repository _repository,
    ExplicitHitlNonImplementationRequestCaptureService? _hitlRequestCapture = null) : IAsyncDisposable
{
    private IAgentSession? session;
    private bool closed;

    public async Task WritePlanAsync(CancellationToken cancellationToken)
    {
        session = await _runtime.OpenSessionAsync(AgentSpecs.PlanAuthoring(_repository), cancellationToken);
        closed = false; // guards the session just opened; a prior session (if any) was already closed by then

        var renderer = new ConsoleTurnRenderer(_console);
        AgentTurnResult result = await session.RunTurnAsync(
            WritePlan.Text,
            renderer.Stream,
            cancellationToken);
        if (result.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new PlanStepException(WithDiagnostics(
                $"WritePlan turn ended in state {result.State}.", result.Diagnostics));
        }

        renderer.EchoIfSilent(result.Output);

        await VerifyPlanGateAsync(result.Diagnostics);
        await CapturePlanHitlRequestsAsync();
    }

    public async Task ReviseAsync(string feedback, CancellationToken cancellationToken)
    {
        if (session is null)
        {
            throw new PlanStepException("ReviseAsync called before a planning session was opened by WritePlanAsync.");
        }

        // Turn 2 on the SAME held-open session — never reopen. The server holds thread state, so this turn
        // sends only the review-feedback delta rather than re-sending the whole transcript.
        var renderer = new ConsoleTurnRenderer(_console);
        AgentTurnResult result = await session.RunTurnAsync(
            RevisePlan.Render(feedback),
            renderer.Stream,
            cancellationToken);
        if (result.State != AgentTurnState.Completed)
        {
            await CloseAsync();
            throw new PlanStepException(WithDiagnostics(
                $"RevisePlan turn ended in state {result.State}.", result.Diagnostics));
        }

        renderer.EchoIfSilent(result.Output);

        await VerifyPlanGateAsync(result.Diagnostics);
        await CapturePlanHitlRequestsAsync();
    }

    // A revise turn that truncates or blanks the plan must not pass — every downstream step seeds from it.
    private async Task VerifyPlanGateAsync(string? diagnostics)
    {
        string? plan = await _artifacts.ReadAsync(OrchestrationArtifactPaths.Plan);
        if (string.IsNullOrWhiteSpace(plan))
        {
            await CloseAsync();
            throw new PlanStepException(WithDiagnostics(
                $"{OrchestrationArtifactPaths.Plan} was not written.", diagnostics));
        }
    }

    // A failed turn's Diagnostics (the codex process's retained stderr tail) rides along in the thrown message
    // so the actual refusal/error text reaches the operator instead of a bare turn state. Copied verbatim from
    // DecisionSession's WithDiagnostics idiom.
    private static string WithDiagnostics(string message, string? diagnostics) =>
        string.IsNullOrWhiteSpace(diagnostics)
            ? message
            : $"{message} Agent stderr (tail):\n{diagnostics}";

    private async Task CapturePlanHitlRequestsAsync()
    {
        if (_hitlRequestCapture is null)
        {
            return;
        }

        string? plan = await _artifacts.ReadAsync(OrchestrationArtifactPaths.Plan);
        if (!string.IsNullOrWhiteSpace(plan))
        {
            await _hitlRequestCapture.CaptureFromSourceAsync(OrchestrationArtifactPaths.Plan, plan);
        }
    }

    // Closed-flag guarded: runtime.CloseSessionAsync fires AT MOST ONCE per opened session. Later calls
    // (whether from a failure path that already closed it, an explicit pipeline close, or DisposeAsync) no-op.
    public async ValueTask CloseAsync()
    {
        if (closed || session is null)
        {
            closed = true;
            return;
        }

        closed = true;
        await _runtime.CloseSessionAsync(session);
    }

    public async ValueTask DisposeAsync() => await CloseAsync();
}
