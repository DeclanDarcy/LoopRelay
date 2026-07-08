namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ExecutionPreparationInputSet(
    ExecutionPreparationManifestInput ActiveEpic,
    IReadOnlyList<ExecutionPreparationManifestInput> MilestoneSpecs,
    ExecutionPreparationManifestInput DecisionLedger);
