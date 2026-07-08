namespace LoopRelay.Roadmap.Cli;

internal enum RoadmapStartupAction
{
    FreshInitialization,
    ResumeActiveWorkflow,
    ReportBlockedWorkflow,
    ReportTerminalWorkflow,
    ReportCompletedWorkflow,
    ReportFailedWorkflow,
}
