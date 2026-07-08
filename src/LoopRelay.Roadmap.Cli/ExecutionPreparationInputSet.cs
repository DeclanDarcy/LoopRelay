namespace LoopRelay.Roadmap.Cli;

internal sealed record ExecutionPreparationInputSet(
    ExecutionPreparationManifestInput ActiveEpic,
    IReadOnlyList<ExecutionPreparationManifestInput> MilestoneSpecs,
    ExecutionPreparationManifestInput DecisionLedger);
