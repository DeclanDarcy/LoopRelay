using CommandCenter.Agents.Abstractions;
using CommandCenter.Agents.Models;
using CommandCenter.Core.Prompts;
using CommandCenter.Core.Repositories;
using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// One execution slice over a HELD-OPEN operational codex session (app-server JSON-RPC over stdio, so the
/// second turn sends only its delta instead of re-sending the whole transcript). Two user-input turns:
/// (1) ContinueExecution renders plan + decisions.md (the execution agent's system prompt) and does the work,
/// and is NOT asked for a handoff; then
/// (2) GenerateHandoff writes .agents/handoffs/handoff.md from the in-session context of turn 1.
/// The session is opened per slice and closed in a finally; the new handoff is verified after turn 2.
/// </summary>
internal sealed class ExecutionStep(
    IAgentRuntime runtime, LoopArtifacts artifacts, ILoopConsole console, Repository repository)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string? plan = await artifacts.ReadPlanAsync();
        (string? decisions, _) = await artifacts.ReadLatestDecisionsAsync();

        // The decision session produces decisions.md (the execution agent's system prompt) before every
        // execution slice, so execution always continues from it — there is no separate first-milestone
        // StartExecution path, and the handoff is consumed by the decision session, not rendered here.
        string executionPrompt = ContinueExecution.Render(plan, decisions);

        IAgentSession session = await runtime.OpenSessionAsync(
            AgentSpecs.Operational(repository, AgentEffortLevel.Medium, identifier: null),
            cancellationToken);
        try
        {
            // Turn 1 - do the work. The prompt no longer asks for a handoff.
            console.Phase("Execution: ContinueExecution");
            AgentTurnResult work = await session.RunTurnAsync(executionPrompt, StreamToConsole, cancellationToken);
            if (work.State != AgentTurnState.Completed)
            {
                throw new LoopStepException($"Execution turn ended in state {work.State}.");
            }

            console.Message(work.Output);

            // Turn 2 - request the handoff on the same held-open session (delta only).
            console.Phase("Execution: GenerateHandoff");
            AgentTurnResult handoffTurn = await session.RunTurnAsync(
                GenerateHandoff.Text, StreamToConsole, cancellationToken);
            if (handoffTurn.State != AgentTurnState.Completed)
            {
                throw new LoopStepException($"Handoff turn ended in state {handoffTurn.State}.");
            }

            console.Message(handoffTurn.Output);

            if (!await artifacts.ExistsAsync(OrchestrationArtifactPaths.LiveHandoff))
            {
                throw new LoopStepException(
                    "Execution completed but .agents/handoffs/handoff.md was not written.");
            }

            console.Info("New handoff.md verified.");
        }
        finally
        {
            await runtime.CloseSessionAsync(session);
        }
    }

    private Task StreamToConsole(AgentStreamChunk chunk)
    {
        if (chunk.Stream == AgentProcessOutputStream.StandardOutput)
        {
            console.Delta(chunk.Content);
        }

        return Task.CompletedTask;
    }
}
