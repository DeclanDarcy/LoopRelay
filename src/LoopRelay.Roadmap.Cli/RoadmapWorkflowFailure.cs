namespace LoopRelay.Roadmap.Cli;

internal sealed record RoadmapWorkflowFailure(
    string JournalEvent,
    RoadmapState OriginatingState,
    RoadmapState AttemptedState,
    RoadmapState FailureState,
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
        RoadmapState originatingState,
        RoadmapState attemptedState,
        RoadmapState failureState,
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
            failureState == RoadmapState.Failed ? TransitionStatus.Failed : TransitionStatus.Paused,
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
