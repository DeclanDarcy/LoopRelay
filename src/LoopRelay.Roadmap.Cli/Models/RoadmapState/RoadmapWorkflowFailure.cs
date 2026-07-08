using LoopRelay.Roadmap.Cli.Models.TransitionInputs;
using LoopRelay.Roadmap.Cli.Primitives.Transitions;

namespace LoopRelay.Roadmap.Cli.Models.RoadmapState;

internal sealed record RoadmapWorkflowFailure(
    string JournalEvent,
    Primitives.State.RoadmapState OriginatingState,
    Primitives.State.RoadmapState AttemptedState,
    Primitives.State.RoadmapState FailureState,
    TransitionStatus StateTransitionStatus,
    string Transition,
    string Projection,
    string PromptContractKey,
    string FailureCategory,
    IReadOnlyList<string> EvidencePaths,
    string Reason,
    string RequiredNextStep,
    string RecoveryIntent,
    string Decision,
    DateTimeOffset FailedAt,
    TransitionInputSnapshot? InputSnapshot = null)
{
    public static RoadmapWorkflowFailure InvariantFailure(
        Primitives.State.RoadmapState originatingState,
        Primitives.State.RoadmapState attemptedState,
        Primitives.State.RoadmapState failureState,
        string transition,
        string projection,
        string failureCategory,
        IReadOnlyList<string> evidencePaths,
        string reason,
        string recoveryGuidance,
        DateTimeOffset failedAt)
    {
        string evidenceList = evidencePaths.Count == 0 ? "validator evidence" : string.Join(", ", evidencePaths);
        string effectiveRecovery = string.IsNullOrWhiteSpace(recoveryGuidance) || recoveryGuidance == "None"
            ? "Repair the invariant violation before continuing the roadmap state machine."
            : recoveryGuidance;
        return new RoadmapWorkflowFailure(
            "InvariantFailed",
            originatingState,
            attemptedState,
            failureState,
            failureState == Primitives.State.RoadmapState.Failed ? TransitionStatus.Failed : TransitionStatus.Paused,
            transition,
            projection,
            "InvariantValidator",
            failureCategory,
            evidencePaths,
            reason,
            $"Review {evidenceList}. {effectiveRecovery} Rerun the roadmap CLI after the invariant violation is repaired.",
            "ResolveInvariantViolation",
            $"Invariant Failed: {failureCategory}",
            failedAt);
    }
}
