namespace LoopRelay.Projections;

public sealed record ProjectionCausalInput(
    string Kind,
    string Identity,
    string Version);
