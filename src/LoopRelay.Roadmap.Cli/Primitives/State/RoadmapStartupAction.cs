namespace LoopRelay.Roadmap.Cli.Primitives;

internal enum RoadmapStartupAction
{
    FreshInitialization,
    ResumeActiveWorkflow,
    ReportBlockedWorkflow,
    ReportTerminalWorkflow,
    ReportCompletedWorkflow,
    ReportFailedWorkflow,
}
