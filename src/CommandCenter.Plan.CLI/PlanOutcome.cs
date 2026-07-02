namespace CommandCenter.Plan.Cli;

/// <summary>Terminal result of a <c>PlanPipeline.RunAsync</c> run; maps to the process exit code in Program.cs.</summary>
internal enum PlanOutcome
{
    Completed,
    PreflightBlocked,
    Failed,
    Cancelled,
}

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
