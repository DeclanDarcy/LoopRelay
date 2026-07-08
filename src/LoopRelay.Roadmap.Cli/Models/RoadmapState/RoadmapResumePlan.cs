using LoopRelay.Roadmap.Cli.Primitives;

namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record RoadmapResumePlan(
    RoadmapResumeAction Action,
    RoadmapState SourceState,
    string Reason,
    RoadmapOutcome? TerminalOutcome = null,
    bool ShouldPersistCoreReady = false)
{
    public static RoadmapResumePlan InitializeCoreReady(string reason) =>
        new(RoadmapResumeAction.ContinueFromCoreReady, RoadmapState.CoreReady, reason, ShouldPersistCoreReady: true);

    public static RoadmapResumePlan ContinueFromCoreReady(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.ContinueFromCoreReady, sourceState, reason);

    public static RoadmapResumePlan SelectNextStrategicInitiative(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.SelectNextStrategicInitiative, sourceState, reason);

    public static RoadmapResumePlan ContinueSelectionDecision(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.ContinueSelectionDecision, sourceState, reason);

    public static RoadmapResumePlan GenerateMilestoneSpecs(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.GenerateMilestoneSpecs, sourceState, reason);

    public static RoadmapResumePlan EvaluateCompletionClaim(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.EvaluateCompletionClaim, sourceState, reason);

    public static RoadmapResumePlan Terminal(RoadmapOutcome outcome, RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.Terminal, sourceState, reason, outcome);

    public static RoadmapResumePlan Block(RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.Block, sourceState, reason, RoadmapOutcome.Paused);
}
