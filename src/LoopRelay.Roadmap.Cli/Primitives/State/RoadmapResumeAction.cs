namespace LoopRelay.Roadmap.Cli.Primitives.State;

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
