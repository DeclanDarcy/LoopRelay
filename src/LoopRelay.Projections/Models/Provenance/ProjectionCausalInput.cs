namespace LoopRelay.Projections.Models;

public sealed record ProjectionCausalInput(
    string Kind,
    string Identity,
    string Version);
