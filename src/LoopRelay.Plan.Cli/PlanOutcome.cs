namespace LoopRelay.Plan.Cli;

/// <summary>Terminal result of a <c>PlanPipeline.RunAsync</c> run; maps to the process exit code in Program.cs.</summary>
internal enum PlanOutcome
{
    Completed,
    PreflightBlocked,
    Failed,
    Cancelled,
}
