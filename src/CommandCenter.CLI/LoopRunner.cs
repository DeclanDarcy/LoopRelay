using CommandCenter.Orchestration;

namespace CommandCenter.Cli;

/// <summary>
/// The LoopStart state machine. Runs serially until the epic completes, a step fails, or cancellation
/// is requested. Owns the warm DecisionSession and disposes it on exit.
/// </summary>
internal sealed class LoopRunner(
    UsageGate usageGate,
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
                // A finished epic needs no Codex work, so check completion BEFORE the usage gate — otherwise a
                // completed epic could block for up to the (potentially multi-day) quota reset before returning.
                if (await gate.IsEpicCompleteAsync())
                {
                    return LoopOutcome.EpicCompleted;
                }

                // ---- Usage gate: block before any Codex work while quota is exhausted. ----
                await usageGate.WaitForCapacityAsync(cancellationToken);

                await artifacts.EnsureOperationalContextAsync();

                bool decisionsExist = await artifacts.ExistsAsync(OrchestrationArtifactPaths.Decisions);
                bool handoffExists = await artifacts.ExistsAsync(OrchestrationArtifactPaths.LiveHandoff);

                if (decisionsExist || !handoffExists)
                {
                    // ---- Branch A: execution then decision ----
                    if (decisionsExist)
                    {
                        await artifacts.RotateLiveDecisionsAsync();
                    }

                    if (handoffExists)
                    {
                        await artifacts.RotateLiveHandoffAsync();
                    }

                    await execution.RunAsync(cancellationToken);
                    await decision.RunAsync(cancellationToken);
                }
                else
                {
                    // ---- Branch B: decision only (resume after an interrupted execution) ----
                    await artifacts.RotateLiveHandoffAsync();
                    await decision.RunAsync(cancellationToken);
                }

                // Both branches verify decisions.md (decision.RunAsync throws if it is missing). Commit and
                // push the working tree, then apply the stall gate: stop if no substantive change recurs.
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
