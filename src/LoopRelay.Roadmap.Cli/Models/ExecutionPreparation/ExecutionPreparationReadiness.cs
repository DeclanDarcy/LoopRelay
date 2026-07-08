namespace LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;

internal sealed record ExecutionPreparationReadiness(
    bool IsFresh,
    string Reason,
    IReadOnlyList<ExecutionPreparationArtifactFreshness> Artifacts);
