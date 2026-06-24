namespace CommandCenter.Workflow.Models;

public sealed class WorkflowContinuationOptions
{
    public bool ContinuationEnabled { get; set; }

    public int ContinuationIntervalSeconds { get; set; } = 60;
}
