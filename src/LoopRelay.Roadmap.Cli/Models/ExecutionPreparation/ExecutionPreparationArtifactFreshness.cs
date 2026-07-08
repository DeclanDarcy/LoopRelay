using LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;

namespace LoopRelay.Roadmap.Cli.Models.ExecutionPreparation;

internal sealed record ExecutionPreparationArtifactFreshness(
    string ArtifactKind,
    string ArtifactIdentity,
    DerivedArtifactFreshness Freshness);
