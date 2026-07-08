namespace LoopRelay.Roadmap.Cli.Primitives;

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
