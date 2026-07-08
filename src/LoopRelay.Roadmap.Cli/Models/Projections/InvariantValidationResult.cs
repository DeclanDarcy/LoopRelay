namespace LoopRelay.Roadmap.Cli.Models.Projections;

internal sealed record InvariantValidationResult(
    bool IsValid,
    Primitives.State.RoadmapState FailureState,
    string? Error,
    string? EvidencePath,
    string FailureCategory,
    string RecoveryGuidance)
{
    public static InvariantValidationResult Valid() => new(true, Primitives.State.RoadmapState.CoreReady, null, null, "None", "None");

    public static InvariantValidationResult Invalid(
        Primitives.State.RoadmapState failureState,
        string error,
        string evidencePath,
        string failureCategory,
        string recoveryGuidance) =>
        new(false, failureState, error, evidencePath, failureCategory, recoveryGuidance);
}
