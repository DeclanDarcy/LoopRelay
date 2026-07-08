namespace LoopRelay.Roadmap.Cli;

internal sealed record ExecutionPreparationReadiness(
    bool IsFresh,
    string Reason,
    IReadOnlyList<ExecutionPreparationArtifactFreshness> Artifacts);
