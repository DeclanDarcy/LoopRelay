namespace LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;

internal sealed record ExecutionPreparationInputSet(
    ExecutionPreparationManifestInput ActiveEpic,
    IReadOnlyList<ExecutionPreparationManifestInput> MilestoneSpecs,
    ExecutionPreparationManifestInput DecisionLedger);
