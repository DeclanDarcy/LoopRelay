namespace LoopRelay.Roadmap.Cli.Models.Projections;

internal sealed record ProjectionCausalInput(
    string Kind,
    string Identity,
    string Version);
