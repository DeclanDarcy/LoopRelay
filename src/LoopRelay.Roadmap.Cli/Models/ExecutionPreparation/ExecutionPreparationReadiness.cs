namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ExecutionPreparationReadiness(
    bool IsFresh,
    string Reason,
    IReadOnlyList<ExecutionPreparationArtifactFreshness> Artifacts);
