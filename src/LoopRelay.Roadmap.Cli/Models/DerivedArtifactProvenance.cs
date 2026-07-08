namespace LoopRelay.Roadmap.Cli.Models;

internal sealed record DerivedArtifactProvenance(
    string ArtifactKind,
    string ArtifactIdentity,
    string Generator,
    IReadOnlyList<DerivedArtifactCausalInput> CausalInputs);
