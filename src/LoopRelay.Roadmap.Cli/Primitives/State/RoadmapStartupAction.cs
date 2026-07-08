namespace LoopRelay.Roadmap.Cli.Primitives.State;

internal enum RoadmapStartupAction
{
    FreshInitialization,
    ResumeActiveWorkflow,
    ReportBlockedWorkflow,
    ReportTerminalWorkflow,
    ReportCompletedWorkflow,
    ReportFailedWorkflow,
}
