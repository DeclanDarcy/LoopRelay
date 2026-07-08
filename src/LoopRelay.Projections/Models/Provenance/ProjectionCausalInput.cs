namespace LoopRelay.Projections.Models.Provenance;

public sealed record ProjectionCausalInput(
    string Kind,
    string Identity,
    string Version);
