using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// The LoopStart state machine. Runs serially until the epic completes, a step fails, or cancellation
/// is requested. Owns the warm DecisionSession and disposes it on exit.
/// </summary>
internal sealed class LoopRunner(
    MilestoneGate gate,
    LoopArtifacts artifacts,
    ExecutionStep execution,
    DecisionSession decision,
    CommitGate commitGate,
    ILoopConsole console) : IAsyncDisposable
{
    public async Task<LoopOutcome> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ---- LoopStart ----
                // A finished epic needs no Codex work, so return immediately. The Codex usage gate now runs
                // per-turn inside GatedAgentRuntime, so a completed epic never opens a session and thus never
                // blocks on a (potentially multi-day) quota reset.
                if (await gate.IsEpicCompleteAsync())
                {
                    return LoopOutcome.EpicCompleted;
                }

                await artifacts.EnsureOperationalContextAsync();

                // ---- Decision-first ----
                // The decision session produces decisions.md — the next execution agent's system prompt. On the
                // first pass (no handoff yet) it generates the first agent's prompt from scratch; afterwards it
                // folds the previous execution session's handoff into the next agent's prompt.
                await decision.RunAsync(cancellationToken);

                // Archive the handoff the decision just consumed so execution writes onto a clean slate
                // (a no-op on the first pass, when no handoff exists yet).
                await artifacts.RotateLiveHandoffAsync();

                // ---- Execution ----
                // Consume decisions.md, do the work, and write the next handoff.
                await execution.RunAsync(cancellationToken);

                // decision.RunAsync verifies decisions.md and execution.RunAsync verifies handoff.md (each throws
                // if its artifact is missing). Commit and push the working tree, then apply the stall gate: stop
                // if no substantive change recurs.
                if (await commitGate.CommitPushAndEvaluateAsync(cancellationToken))
                {
                    return LoopOutcome.Stalled;
                }
            }
        }
        catch (OperationCanceledException)
        {
            console.Warn("Cancellation requested — stopping the loop.");
            return LoopOutcome.Cancelled;
        }
        catch (LoopStepException ex)
        {
            console.Error(ex.Message);
            return LoopOutcome.Failed;
        }
    }

    public ValueTask DisposeAsync() => decision.DisposeAsync();
}
