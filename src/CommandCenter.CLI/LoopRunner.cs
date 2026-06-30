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
                if (await gate.IsEpicCompleteAsync())
                {
                    return LoopOutcome.EpicCompleted;
                }

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

    public async ValueTask DisposeAsync() => await decision.DisposeAsync();
}
