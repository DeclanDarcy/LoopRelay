namespace LoopRelay.Roadmap.Cli;

internal sealed record DerivedArtifactProvenance(
    string ArtifactKind,
    string ArtifactIdentity,
    string Generator,
    IReadOnlyList<DerivedArtifactCausalInput> CausalInputs);
