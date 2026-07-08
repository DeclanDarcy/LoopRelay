namespace LoopRelay.Plan.Cli;

/// <summary>
/// Thrown by a deterministic verification gate when a pipeline step's expected filesystem effect did not happen.
/// Aborts the pipeline; nothing is retried. Callers append the agent stderr tail (<c>AgentTurnResult.Diagnostics</c>)
/// to the message when present, via the <c>WithDiagnostics</c> idiom copied from the reference CLI.
/// </summary>
internal sealed class PlanStepException : Exception
{
    public PlanStepException(string message)
        : base(message)
    {
    }

    public PlanStepException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
