namespace LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;

internal sealed record DerivedArtifactProvenance(
    string ArtifactKind,
    string ArtifactIdentity,
    string Generator,
    IReadOnlyList<DerivedArtifactCausalInput> CausalInputs);
