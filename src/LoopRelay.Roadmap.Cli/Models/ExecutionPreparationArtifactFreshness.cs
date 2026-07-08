namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record ExecutionPreparationArtifactFreshness(
    string ArtifactKind,
    string ArtifactIdentity,
    DerivedArtifactFreshness Freshness);
