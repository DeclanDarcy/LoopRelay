namespace LoopRelay.Roadmap.Cli;

internal enum DerivedArtifactStaleReason
{
    MissingManifest,
    UnknownProvenance,
    Superseded,
    ArtifactKindDrift,
    ArtifactIdentityDrift,
    GeneratorDrift,
    ArtifactMissing,
    ArtifactHashDrift,
    ActiveEpicDrift,
    MilestoneSpecDrift,
    DecisionLedgerDrift,
    OperationalContextDrift,
    ExecutionPromptDrift,
    CausalInputDrift,
    UnexpectedActiveArtifact,
    SelectionCycleDrift,
    SelectionProjectionDrift,
    SelectionPromptContextDrift,
    SelectionSecondaryInputDrift,
    RoadmapCompletionContextDrift,
    RoadmapSourceDrift,
    RetiredEpicStateDrift,
}
