namespace LoopRelay.Roadmap.Cli;

internal sealed record ExecutionPreparationArtifactFreshness(
    string ArtifactKind,
    string ArtifactIdentity,
    DerivedArtifactFreshness Freshness);
