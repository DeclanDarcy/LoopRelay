namespace LoopRelay.Roadmap.Cli.Models.DerivedArtifacts;

internal sealed record DerivedArtifactCausalInput(
    string Kind,
    string Identity,
    string Version);
