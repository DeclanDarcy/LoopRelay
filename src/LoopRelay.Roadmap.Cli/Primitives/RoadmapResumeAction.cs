namespace LoopRelay.Roadmap.Cli;

internal enum RoadmapResumeAction
{
    ContinueFromCoreReady,
    SelectNextStrategicInitiative,
    ContinueSelectionDecision,
    GenerateMilestoneSpecs,
    EvaluateCompletionClaim,
    Terminal,
    Block,
}
