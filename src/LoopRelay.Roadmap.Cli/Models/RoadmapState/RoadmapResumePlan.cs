using LoopRelay.Roadmap.Cli.Primitives.State;

namespace LoopRelay.Roadmap.Cli.Models.RoadmapState;

internal sealed record RoadmapResumePlan(
    RoadmapResumeAction Action,
    Primitives.State.RoadmapState SourceState,
    string Reason,
    RoadmapOutcome? TerminalOutcome = null,
    bool ShouldPersistCoreReady = false)
{
    public static RoadmapResumePlan InitializeCoreReady(string reason) =>
        new(RoadmapResumeAction.ContinueFromCoreReady, Primitives.State.RoadmapState.CoreReady, reason, ShouldPersistCoreReady: true);

    public static RoadmapResumePlan ContinueFromCoreReady(Primitives.State.RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.ContinueFromCoreReady, sourceState, reason);

    public static RoadmapResumePlan SelectNextStrategicInitiative(Primitives.State.RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.SelectNextStrategicInitiative, sourceState, reason);

    public static RoadmapResumePlan ContinueSelectionDecision(Primitives.State.RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.ContinueSelectionDecision, sourceState, reason);

    public static RoadmapResumePlan GenerateMilestoneSpecs(Primitives.State.RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.GenerateMilestoneSpecs, sourceState, reason);

    public static RoadmapResumePlan EvaluateCompletionClaim(Primitives.State.RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.EvaluateCompletionClaim, sourceState, reason);

    public static RoadmapResumePlan Terminal(RoadmapOutcome outcome, Primitives.State.RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.Terminal, sourceState, reason, outcome);

    public static RoadmapResumePlan Block(Primitives.State.RoadmapState sourceState, string reason) =>
        new(RoadmapResumeAction.Block, sourceState, reason, RoadmapOutcome.Paused);
}
