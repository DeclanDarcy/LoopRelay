namespace LoopRelay.Roadmap.Cli;

internal sealed record InvariantValidationResult(
    bool IsValid,
    RoadmapState FailureState,
    string? Error,
    string? EvidencePath,
    string FailureCategory,
    string RecoveryGuidance)
{
    public static InvariantValidationResult Valid() => new(true, RoadmapState.CoreReady, null, null, "None", "None");

    public static InvariantValidationResult Invalid(
        RoadmapState failureState,
        string error,
        string evidencePath,
        string failureCategory,
        string recoveryGuidance) =>
        new(false, failureState, error, evidencePath, failureCategory, recoveryGuidance);
}
