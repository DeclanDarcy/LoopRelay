using LoopRelay.Agents.Abstractions;
using LoopRelay.Agents.Models;
using LoopRelay.Agents.Primitives;
using LoopRelay.Cli.Abstractions;
using LoopRelay.Cli.Models;
using LoopRelay.Core.Models.Repositories;
using LoopRelay.Core.Prompts;
using LoopRelay.Orchestration.Services;
using LoopRelay.Orchestration.Services.NonImplementationReview;

namespace LoopRelay.Cli.Services;

/// <summary>
/// One execution slice over a HELD-OPEN operational codex session (app-server JSON-RPC over stdio, so the
/// second turn sends only its delta instead of re-sending the whole transcript). Two user-input turns:
/// (1) the work turn — StartExecution renders the plan (plus its optional .agents/details.md addendum) on the
/// FIRST execution (no decisions.md yet), else ContinueExecution renders plan + details + decisions.md (the
/// execution agent's system prompt the decision session produced this slice) — and is NOT asked for a handoff;
/// then (2) the handoff turn writes .agents/handoffs/handoff.md from the in-session context of turn 1.
/// WHICH prompt turn 2 gets is decided by change detection run AFTER turn 1 (so the work turn's own writes
/// count): a real (non-.agents) working-tree change means the ordinary GenerateHandoff; a clean tree means
/// GenerateNoChangesHandoff, which confronts the agent with the milestone items still unticked — fresh by
/// construction, since ticking a box during turn 1 advanced that file's stamp and forces the gate's re-parse
/// — and demands an explanation or a permission request instead of a routine progress handoff.
/// The session is opened per slice and closed in a finally; the new handoff is verified after turn 2.
/// </summary>
internal sealed class ExecutionStep(
    IAgentRuntime runtime,
    LoopArtifacts artifacts,
    ILoopConsole console,
    Repository repository,
    WorkingTreeChangeDetector changeDetector,
    MilestoneGate milestones,
    string? promptPolicy = null)
{
    private readonly string promptPolicy = promptPolicy ?? ImplementationFirstPromptPolicyComposer.ComposeDefault();

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        string? plan = await artifacts.ReadPlanAsync();
        // .agents/details.md is the optional plan addendum, injected directly after the plan in every execution
        // prompt (read like the plan: null when absent, rendered as empty) so a non-self-contained plan carries
        // its detail inline and the agent never has to chase the file on disk.
        string? details = await artifacts.ReadDetailsAsync();

        // First execution of a fresh plan (no decisions.md) starts straight from the plan via StartExecution —
        // the self-contained plan is context enough to get going. Once a decision has produced decisions.md (the
        // execution agent's system prompt), execution CONTINUES from it. The handoff is consumed by the decision
        // session, not rendered here.
        bool hasDecisions = await artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions);
        string executionPrompt;
        string workPhase;
        if (hasDecisions)
        {
            (string? decisions, _) = await artifacts.ReadLatestDecisionsAsync();
            executionPrompt = ImplementationFirstPromptPolicyComposer.AppendPromptPolicy(
                ContinueExecution.Render(plan, details, decisions),
                promptPolicy);
            workPhase = "Execution: ContinueExecution";
        }
        else
        {
            executionPrompt = ImplementationFirstPromptPolicyComposer.AppendPromptPolicy(
                StartExecution.Render(plan, details),
                promptPolicy);
            workPhase = "Execution: StartExecution";
        }

        // The execution agent runs codex with full access (no sandbox): it needs to build, run tools, touch
        // git, and reach outside the workspace to do real work. This is the ONE session granted danger-full-access
        // — the decision session stays read-only and the context-update evolution keeps its own posture.
        IAgentSession session = await runtime.OpenSessionAsync(
            AgentSpecs.Operational(
                repository, AgentEffortLevel.Medium, identifier: null, sandboxIdentifier: "danger-full-access"),
            cancellationToken);
        try
        {
            // Turn 1 - do the work. The prompt no longer asks for a handoff.
            console.Phase(workPhase);
            var workRenderer = new ConsoleTurnRenderer(console);
            AgentTurnResult work = await session.RunTurnAsync(executionPrompt, workRenderer.Stream, cancellationToken);
            if (work.State != AgentTurnState.Completed)
            {
                throw new LoopStepException($"Execution turn ended in state {work.State}.");
            }

            workRenderer.EchoIfSilent(work.Output);

            // Change detection BEFORE handoff generation: what turn 1 actually left in the working tree
            // decides which handoff is asked for. "No changes" is CommitGate's exact progress rule (no
            // real, non-.agents paths), so the handoff and the end-of-iteration stall gate can never tell
            // different stories about the same slice. An EMPTY unticked list still takes the no-changes
            // path — the rule is purely change-detection; the prompt then simply has no items to enumerate.
            IReadOnlyList<string> changed = await changeDetector.GetRealChangedPathsAsync();
            string handoffPrompt;
            string handoffPhase;
            if (changed.Count > 0)
            {
                handoffPrompt = GenerateHandoff.Text;
                handoffPhase = "Execution: GenerateHandoff";
            }
            else
            {
                IReadOnlyList<string> unticked = await milestones.GetUntickedItemsAsync();
                handoffPrompt = GenerateNoChangesHandoff.Render(string.Join("\n", unticked));
                handoffPhase = "Execution: GenerateNoChangesHandoff";
            }

            // Turn 2 - request the handoff on the same held-open session (delta only).
            console.Phase(handoffPhase);
            var handoffRenderer = new ConsoleTurnRenderer(console);
            AgentTurnResult handoffTurn = await session.RunTurnAsync(
                handoffPrompt, handoffRenderer.Stream, cancellationToken);
            if (handoffTurn.State != AgentTurnState.Completed)
            {
                throw new LoopStepException($"Handoff turn ended in state {handoffTurn.State}.");
            }

            handoffRenderer.EchoIfSilent(handoffTurn.Output);

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
}
